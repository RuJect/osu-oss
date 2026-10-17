// authredirect.c — integrity-measurement redirect shim for the osu! auth transplant.
//
// The proprietary native auth module ("AuthNative") measures a fixed set of game
// assemblies and binds the measurement into every signing token (x-token /
// X-Osu-Version-Hash). It resolves the directory from the *executable's own
// location* (Linux: readlink("/proc/self/exe"); macOS: _NSGetExecutablePath();
// Windows: GetModuleFileNameW) and then reads each file from there. A rebuilt
// (OSS) tree therefore fails server-side verification ("invalid client hash" /
// "not supported on this version").
//
// This shim redirects ONLY the native module's own reads of those assemblies to
// pristine official copies (in AUTH_OFFICIAL_DIR, set by the managed loader).
// Every other caller is untouched.
//
// Per-platform strategy (all share the same policy: basename in the measured set
// + caller is the AuthNative image + pristine copy exists):
//   Linux   : LD_PRELOAD; interposes fopen/open/open64/openat/openat64 and
//             detects the caller via a stack walk (backtrace + dladdr).
//   macOS   : DYLD_INSERT_LIBRARIES; interposes fopen/open/openat via the
//             __DATA,__interpose section; caller via backtrace + dladdr.
//   Windows : injected/loaded DLL; hooks AuthNative.dll's *own* import table
//             entries for CreateFileW / GetFileAttributesExW (installed on DLL
//             load notification), so no caller check is required.
//
// Config:  AUTH_OFFICIAL_DIR=<dir>   (absolute path of the pristine copies)
// Logging: AUTHREDIRECT_LOG=<file>   (optional)
//
// Loading:
//   Linux   : the managed RedirectBootstrap re-executes the process with
//             LD_PRELOAD=/path/authredirect.so, so no prelude is required. A manual
//             LD_PRELOAD / an OSU_AUTHREDIRECT override still works.
//   macOS   : likewise via DYLD_INSERT_LIBRARIES=/path/authredirect.dylib
//             (the app must be signed with
//              com.apple.security.cs.allow-dyld-environment-variables).
//   Windows : authredirect.dll next to osu!.exe; the managed auth loader loads it
//             before constructing the transplant.
//
// Build:
//   Linux   : gcc -shared -fPIC -O2 -o authredirect.so authredirect.c -ldl
//   macOS   : clang -dynamiclib -O2 -o authredirect.dylib authredirect.c
//   Windows : cl x64 authredirect.c /LD /O2 /link /OUT:authredirect.dll
//             (or x86_64-w64-mingw32-gcc -shared -O2 -o authredirect.dll authredirect.c)

#if defined(__linux__) && !defined(_GNU_SOURCE)
#define _GNU_SOURCE
#endif

// Thread-local storage keyword (MSVC vs GCC/Clang).
#if defined(_MSC_VER)
#define AUTH_TLS __declspec(thread)
#else
#define AUTH_TLS __thread
#endif

// Assemblies measured by the native auth module.
#define AUTH_MEASURED_LIST(X) \
    X("osu!.dll") \
    X("osu.Framework.dll") \
    X("osu.Game.dll") \
    X("osu.Game.Rulesets.Catch.dll") \
    X("osu.Game.Rulesets.Mania.dll") \
    X("osu.Game.Rulesets.Osu.dll") \
    X("osu.Game.Rulesets.Taiko.dll")

#define AUTH_WIDEN(x) L##x

// ---------------------------------------------------------------------------
// Windows
// ---------------------------------------------------------------------------
#if defined(_WIN32)

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winternl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define MEASURED_MAX 32768

static const wchar_t* const measured_w[] = {
#define X(n) AUTH_WIDEN(n),
    AUTH_MEASURED_LIST(X)
#undef X
    NULL,
};

static wchar_t official_dir[MEASURED_MAX];
static int dir_ready = 0;
static int in_hook = 0;

static HANDLE(WINAPI* real_CreateFileW)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE) = NULL;
static BOOL(WINAPI* real_GetFileAttributesExW)(LPCWSTR, GET_FILEEX_INFO_LEVELS, LPVOID) = NULL;

static void note(const wchar_t* what, const wchar_t* path)
{
    const char* logp = getenv("AUTHREDIRECT_LOG");
    if (!logp || !logp[0])
        return;

    FILE* f = fopen(logp, "a");
    if (f)
    {
        fwprintf(f, L"%lu %ls %ls\n", (unsigned long)GetCurrentProcessId(), what, path);
        fclose(f);
    }
}

static const wchar_t* base_name_w(const wchar_t* path)
{
    const wchar_t* a = wcsrchr(path, L'\\');
    const wchar_t* b = wcsrchr(path, L'/');
    const wchar_t* c = a > b ? a : b;
    return c ? c + 1 : path;
}

static int is_measured_w(const wchar_t* path)
{
    const wchar_t* name = base_name_w(path);

    for (int i = 0; measured_w[i]; i++)
    {
        if (_wcsicmp(name, measured_w[i]) == 0)
            return 1;
    }

    return 0;
}

static void ensure_dir(void)
{
    if (dir_ready)
        return;

    dir_ready = 1;

    DWORD n = GetEnvironmentVariableW(L"AUTH_OFFICIAL_DIR", official_dir,
        (DWORD)(sizeof(official_dir) / sizeof(wchar_t)));

    if (n > 0 && n < (DWORD)(sizeof(official_dir) / sizeof(wchar_t)))
    {
        note(L"dir-env", official_dir);
        return;
    }

    note(L"dir-env-missing", L"AUTH_OFFICIAL_DIR");

    wchar_t exe[MEASURED_MAX];
    DWORD len = GetModuleFileNameW(NULL, exe, (DWORD)(sizeof(exe) / sizeof(wchar_t)));

    if (len > 0 && len < (DWORD)(sizeof(exe) / sizeof(wchar_t)))
    {
        wchar_t* slash = wcsrchr(exe, L'\\');

        if (slash)
        {
            lstrcpyW(slash + 1, L"Auth\\Official");
            lstrcpynW(official_dir, exe, (int)(sizeof(official_dir) / sizeof(wchar_t)));
            return;
        }
    }

    official_dir[0] = 0;
}

// Returns replacement path or NULL to pass through.
static const wchar_t* redirect(const wchar_t* path, const wchar_t* func)
{
    static AUTH_TLS wchar_t buf[MEASURED_MAX];

    if (in_hook)
        return NULL;

    if (!path || !is_measured_w(path))
        return NULL;

    ensure_dir();

    if (!official_dir[0])
    {
        note(L"no-official-dir", path);
        return NULL;
    }

    swprintf(buf, MEASURED_MAX, L"%ls\\%ls", official_dir, base_name_w(path));

    if (GetFileAttributesW(buf) == INVALID_FILE_ATTRIBUTES)
    {
        note(L"no-official-file", buf);
        return NULL;
    }

    in_hook = 1;
    note(func, path);
    in_hook = 0;

    return buf;
}

static HANDLE WINAPI hook_CreateFileW(LPCWSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES sa,
                                      DWORD disposition, DWORD flags, HANDLE templ)
{
    if (!real_CreateFileW)
        real_CreateFileW = (void*)GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "CreateFileW");

    if (!real_CreateFileW)
        return INVALID_HANDLE_VALUE;

    const wchar_t* p = redirect(name, L"CreateFileW");

    if (p)
        name = p;

    return real_CreateFileW(name, access, share, sa, disposition, flags, templ);
}

static BOOL WINAPI hook_GetFileAttributesExW(LPCWSTR name, GET_FILEEX_INFO_LEVELS level, LPVOID info)
{
    if (!real_GetFileAttributesExW)
        real_GetFileAttributesExW = (void*)GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "GetFileAttributesExW");

    if (!real_GetFileAttributesExW)
        return FALSE;

    const wchar_t* p = redirect(name, L"GetFileAttributesExW");

    if (p)
        name = p;

    return real_GetFileAttributesExW(name, level, info);
}

// Case-insensitive basename comparison of a narrow import descriptor name.
static int import_dll_matches(const char* descriptor, const char* wanted)
{
    const char* a = strrchr(descriptor, '\\');
    const char* b = strrchr(descriptor, '/');
    const char* base = a > b ? a : b;
    base = base ? base + 1 : descriptor;
    return _stricmp(base, wanted) == 0;
}

static int patch_iat(HMODULE mod, const char* import_dll, const char* func, void* hook)
{
    if (!mod)
        return 0;

    BYTE* base = (BYTE*)mod;
    IMAGE_DOS_HEADER* dos = (IMAGE_DOS_HEADER*)base;

    if (dos->e_magic != IMAGE_DOS_SIGNATURE)
        return 0;

    IMAGE_NT_HEADERS* nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);

    if (nt->Signature != IMAGE_NT_SIGNATURE)
        return 0;

    IMAGE_DATA_DIRECTORY impdir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];

    if (!impdir.VirtualAddress)
        return 0;

    IMAGE_IMPORT_DESCRIPTOR* imp = (IMAGE_IMPORT_DESCRIPTOR*)(base + impdir.VirtualAddress);

    for (; imp->Name; imp++)
    {
        if (!import_dll_matches((const char*)(base + imp->Name), import_dll))
            continue;

        // Only name-based imports can be matched.
        if (!imp->OriginalFirstThunk)
            continue;

        IMAGE_THUNK_DATA* orig = (IMAGE_THUNK_DATA*)(base + imp->OriginalFirstThunk);
        IMAGE_THUNK_DATA* first = (IMAGE_THUNK_DATA*)(base + imp->FirstThunk);

        for (int i = 0; first[i].u1.Function; i++)
        {
            if (!orig[i].u1.AddressOfData)
                continue;

            if (orig[i].u1.Ordinal & IMAGE_ORDINAL_FLAG64)
                continue;

            IMAGE_IMPORT_BY_NAME* ibn = (IMAGE_IMPORT_BY_NAME*)(base + (DWORD_PTR)orig[i].u1.AddressOfData);

            if (strcmp((const char*)ibn->Name, func) != 0)
                continue;

            DWORD old;

            if (VirtualProtect(&first[i].u1.Function, sizeof(void*), PAGE_READWRITE, &old))
            {
                first[i].u1.Function = (ULONG_PTR)hook;
                VirtualProtect(&first[i].u1.Function, sizeof(void*), old, &old);
                return 1;
            }

            return 0;
        }
    }

    return 0;
}

static void patch_module(HMODULE mod)
{
    patch_iat(mod, "KERNEL32.dll", "CreateFileW", (void*)hook_CreateFileW);
    patch_iat(mod, "KERNEL32.dll", "GetFileAttributesExW", (void*)hook_GetFileAttributesExW);
}

typedef struct
{
    ULONG Flags;
    PCUNICODE_STRING FullDllName;
    PCUNICODE_STRING BaseDllName;
    PVOID DllBase;
    ULONG SizeOfImage;
} LDR_DLL_LOADED_NOTIFICATION_DATA;

typedef union
{
    LDR_DLL_LOADED_NOTIFICATION_DATA Loaded;
} LDR_DLL_NOTIFICATION_DATA;

#define LDR_DLL_NOTIFICATION_REASON_LOADED 1

typedef VOID(NTAPI* LDR_DLL_NOTIFICATION_FUNCTION)(ULONG, const LDR_DLL_NOTIFICATION_DATA*, PVOID);

static VOID NTAPI dll_notification(ULONG reason, const LDR_DLL_NOTIFICATION_DATA* data, PVOID context)
{
    (void)context;

    if (reason != LDR_DLL_NOTIFICATION_REASON_LOADED)
        return;

    PCUNICODE_STRING name = data->Loaded.BaseDllName;

    if (name && name->Buffer && name->Length == 14 * (USHORT)sizeof(wchar_t)
        && _wcsnicmp(name->Buffer, L"AuthNative.dll", 14) == 0)
        patch_module((HMODULE)data->Loaded.DllBase);
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;

    if (reason != DLL_PROCESS_ATTACH)
        return TRUE;

    DisableThreadLibraryCalls(instance);

    HMODULE already = GetModuleHandleW(L"AuthNative.dll");

    if (already)
        patch_module(already);

    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");

    if (ntdll)
    {
        typedef NTSTATUS(NTAPI* LdrRegisterDllNotificationFn)(ULONG, LDR_DLL_NOTIFICATION_FUNCTION, PVOID, PVOID*);
        LdrRegisterDllNotificationFn reg = (LdrRegisterDllNotificationFn)(void*)GetProcAddress(ntdll, "LdrRegisterDllNotification");

        if (reg)
        {
            PVOID cookie;
            reg(0, dll_notification, NULL, &cookie);
        }
    }

    return TRUE;
}

// ---------------------------------------------------------------------------
// POSIX (Linux + macOS)
// ---------------------------------------------------------------------------
#else

#include <dlfcn.h>
#include <stdarg.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <unistd.h>
#include <limits.h>
#include <stdint.h>
#include <execinfo.h>
#include <sys/stat.h>
#include <fcntl.h>

#if defined(__APPLE__)
#include <mach-o/dyld.h>
#endif

static const char* const measured[] = {
#define X(n) n,
    AUTH_MEASURED_LIST(X)
#undef X
    NULL,
};

static char official_dir[PATH_MAX] = "";
static int dir_ready = 0;
static __thread int in_hook = 0;

static void note(const char* what, const char* path);

static void ensure_dir(void)
{
    if (dir_ready)
        return;

    dir_ready = 1;

    const char* env = getenv("AUTH_OFFICIAL_DIR");

    if (env && env[0])
    {
        snprintf(official_dir, sizeof official_dir, "%s", env);
        note("dir-env", official_dir);
        return;
    }

    note("dir-env-missing", "AUTH_OFFICIAL_DIR");

#if defined(__APPLE__)
    char exe[PATH_MAX];
    uint32_t size = sizeof exe;

    if (_NSGetExecutablePath(exe, &size) == 0)
    {
        char* slash = strrchr(exe, '/');

        if (slash)
        {
            snprintf(official_dir, sizeof official_dir, "%.*s/Auth/Official", (int)(slash - exe), exe);
            return;
        }
    }
#else
    ssize_t n = readlink("/proc/self/exe", official_dir, sizeof(official_dir) - 32);

    if (n > 0 && (size_t)n < sizeof(official_dir) - 32)
    {
        official_dir[n] = 0;
        char* slash = strrchr(official_dir, '/');

        if (slash)
        {
            strcpy(slash + 1, "Auth/Official");
            return;
        }
    }
#endif

    official_dir[0] = 0;
}

static const char* base_name(const char* path)
{
    const char* b = strrchr(path, '/');
    return b ? b + 1 : path;
}

static int is_measured(const char* path)
{
    const char* b = base_name(path);

    for (int i = 0; measured[i]; i++)
    {
        if (!strcmp(b, measured[i]))
            return 1;
    }

    return 0;
}

// True when any live caller frame lives inside the native auth module.
// (Frame-walking is robust against inlining/PLT wrappers.)
static int called_from_authnative(void)
{
    void* frames[32];
    int n = backtrace(frames, 32);

    for (int i = 0; i < n; i++)
    {
        Dl_info info;

        if (dladdr(frames[i], &info) && info.dli_fname)
        {
            if (strstr(info.dli_fname, "AuthNative"))
                return 1;
        }
    }

    return 0;
}

static void note(const char* what, const char* path)
{
    const char* logp = getenv("AUTHREDIRECT_LOG");

    if (!logp || !logp[0])
        return;

    FILE* f = fopen(logp, "a");

    if (f)
    {
        fprintf(f, "%d %s %s\n", getpid(), what, path);
        fclose(f);
    }
}

// Returns replacement path (static per-thread buffer) or NULL to call through.
static const char* redirect(const char* path, const char* func)
{
    if (in_hook)
        return NULL;

    if (!path || !is_measured(path))
        return NULL;

    if (!called_from_authnative())
        return NULL;

    ensure_dir();

    if (!official_dir[0])
    {
        note("no-official-dir", path);
        return NULL;
    }

    static __thread char buf[PATH_MAX];
    snprintf(buf, sizeof buf, "%s/%s", official_dir, base_name(path));

    if (access(buf, R_OK) != 0)
    {
        note("no-official-file", buf);
        return NULL;
    }

    in_hook = 1;
    note(func, path);
    in_hook = 0;

    return buf;
}

#if defined(__APPLE__)

typedef struct
{
    const void* replacement;
    const void* replacee;
} auth_interpose_tuple;

#define DYLD_INTERPOSE(replacement, replacee) \
    __attribute__((used)) auth_interpose_tuple \
    _interpose_##replacee __attribute__((section("__DATA,__interpose"))) = \
        { (const void*)(uintptr_t)&(replacement), (const void*)(uintptr_t)&(replacee) };

// The interpose tuples carry the original implementation in their replacee
// field. dlsym(RTLD_NEXT, ...) must NOT be used to resolve it: with DYLD
// interposing the lookup resolves back to the interposer itself, which would
// make the wrappers recurse into themselves forever.
extern auth_interpose_tuple _interpose_fopen;
extern auth_interpose_tuple _interpose_open;
extern auth_interpose_tuple _interpose_openat;

static FILE* interposed_fopen(const char* path, const char* mode)
{
    static FILE* (*real)(const char*, const char*) = NULL;
    if (!real) real = (FILE* (*)(const char*, const char*))_interpose_fopen.replacee;
    const char* p = redirect(path, "fopen");
    if (p) path = p;
    return real(path, mode);
}

static int interposed_open(const char* path, int flags, ...)
{
    static int (*real)(const char*, int, ...) = NULL;
    if (!real) real = (int (*)(const char*, int, ...))_interpose_open.replacee;
    const char* p = redirect(path, "open");
    if (p) path = p;
    if (flags & O_CREAT)
    {
        va_list ap; va_start(ap, flags);
        mode_t m = va_arg(ap, mode_t);
        va_end(ap);
        return real(path, flags, m);
    }
    return real(path, flags);
}

static int interposed_openat(int dirfd, const char* path, int flags, ...)
{
    static int (*real)(int, const char*, int, ...) = NULL;
    if (!real) real = (int (*)(int, const char*, int, ...))_interpose_openat.replacee;
    const char* p = redirect(path, "openat");
    if (p) { dirfd = AT_FDCWD; path = p; }
    if (flags & O_CREAT)
    {
        va_list ap; va_start(ap, flags);
        mode_t m = va_arg(ap, mode_t);
        va_end(ap);
        return real(dirfd, path, flags, m);
    }
    return real(dirfd, path, flags);
}

DYLD_INTERPOSE(interposed_fopen, fopen)
DYLD_INTERPOSE(interposed_open, open)
DYLD_INTERPOSE(interposed_openat, openat)

#else // Linux: LD_PRELOAD symbol interposition

int open(const char* path, int flags, ...)
{
    static int (*real_open)(const char*, int, ...) = NULL;
    if (!real_open) real_open = dlsym(RTLD_NEXT, "open");
    const char* r = redirect(path, "open");
    if (r) path = r;
    if (flags & O_CREAT) { va_list ap; va_start(ap, flags); mode_t m = va_arg(ap, mode_t); va_end(ap); return real_open(path, flags, m); }
    return real_open(path, flags);
}

int open64(const char* path, int flags, ...)
{
    static int (*r)(const char*, int, ...) = NULL;
    if (!r) r = dlsym(RTLD_NEXT, "open64");
    const char* p = redirect(path, "open64");
    if (p) path = p;
    if (flags & O_CREAT) { va_list ap; va_start(ap, flags); mode_t m = va_arg(ap, mode_t); va_end(ap); return r(path, flags, m); }
    return r(path, flags);
}

int openat(int dirfd, const char* path, int flags, ...)
{
    static int (*r)(int, const char*, int, ...) = NULL;
    if (!r) r = dlsym(RTLD_NEXT, "openat");
    const char* p = redirect(path, "openat");
    if (p) { dirfd = AT_FDCWD; path = p; }
    if (flags & O_CREAT) { va_list ap; va_start(ap, flags); mode_t m = va_arg(ap, mode_t); va_end(ap); return r(dirfd, path, flags, m); }
    return r(dirfd, path, flags);
}

int openat64(int dirfd, const char* path, int flags, ...)
{
    static int (*r)(int, const char*, int, ...) = NULL;
    if (!r) r = dlsym(RTLD_NEXT, "openat64");
    const char* p = redirect(path, "openat64");
    if (p) { dirfd = AT_FDCWD; path = p; }
    if (flags & O_CREAT) { va_list ap; va_start(ap, flags); mode_t m = va_arg(ap, mode_t); va_end(ap); return r(dirfd, path, flags, m); }
    return r(dirfd, path, flags);
}

FILE* fopen(const char* path, const char* mode)
{
    static FILE* (*r)(const char*, const char*) = NULL;
    if (!r) r = dlsym(RTLD_NEXT, "fopen");
    const char* p = redirect(path, "fopen");
    if (p) path = p;
    return r(path, mode);
}

FILE* fopen64(const char* path, const char* mode)
{
    static FILE* (*r)(const char*, const char*) = NULL;
    if (!r) r = dlsym(RTLD_NEXT, "fopen64");
    const char* p = redirect(path, "fopen64");
    if (p) path = p;
    return r(path, mode);
}

#endif // __APPLE__ / Linux

#endif // _WIN32
