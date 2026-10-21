<p align="center">
  <img width="500" alt="osu! logo" src="assets/lazer.png">
</p>

# osu!oss

A source-built osu! client that connects to the official servers without hooks or runtime patching.

## Before you start

osu!oss is an unofficial fork of osu!lazer and is not affiliated with ppy Pty Ltd.

Using a modified client violates the osu! Terms of Service. Account restrictions are possible. The risk is lower than with binary patching or runtime modification, because osu!oss does not inject into an installed official client or patch it after launch, but the risk is not zero. You use osu!oss at your own risk.

osu!oss contains no cheats and will not add them. Contributions that add gameplay advantages, gameplay automation, or other cheating functionality are not accepted.

## What it is

osu!oss is built from source. The client itself is changed, not an installed official binary. Authentication is implemented in `osu.Game.Auth` and uses the official client's managed auth module with pristine assemblies fetched from the matching osu! release.

As far as we know, this is the first public project to run a source-modified osu! client against the official servers this way.

## Status

Early development. There are no installers or prebuilt packages yet; the client is built and run from source. The `transplant` authentication path works. The `native` path does not (see below).

## Current features

- **Official-server authentication** via `transplant` mode, including the first-run download of the required official files.
- **osu!oss settings section**: incompatible mods, system mods, "Download all favourites", break skipping, and auto-skip for breaks, intro/outro, or both.
- **Layout controls**: toolbar position and inversion, notification panel and toast positions, settings panel on either side, muted and transparent notifications.
- **Themes**: 11 built-in themes, custom JSON themes, glass blur, and aggressive tinting. The default is Midnight (Violet).
- **Skin cursor** for the menu.
- **JavaScript scripting**: editor, console (`Ctrl+F4`), persistent script storage, event and notification APIs.
- **Command palette** (`Ctrl+Space`) with fuzzy search, argument hints, and built-in commands such as `search`, `mods`, `skin`, `ruleset`, `randommap`, and `exit`.

## Authentication (`osu.Game.Auth`)

The module installs `APIRequest.Sign` and `HubClientConnector.Sign`, so requests and SignalR connections are signed in the same places as in the official client. The signing method is selected with `OSU_AUTH_METHOD`.

### `transplant` (default, working)

- Fetches pristine, version-matched official assemblies from the matching GitHub release: the AppImage on Linux, the app ZIP on macOS, and the Squirrel package on Windows. The measured game assemblies are not shipped in this repository.
- Caches them under `<osu! storage>/osu-oss/auth-blobs` and disables signing until the set is complete. The first launch downloads them in the background; restart after it finishes.
- Loads the managed auth module shipped next to the executable as `osu.Game.Auth.transplant.dll` and delegates signing to it.
- Redirects the module's measurements to the pristine files with the `authredirect` shim. On Linux and macOS the game re-executes itself with `LD_PRELOAD`/`DYLD_INSERT_LIBRARIES`; on Windows the shim is loaded into the process.
- On Linux, the module runs under a seccomp user-notification filter with a separate supervisor process. Process creation, tracing, namespaces, mounts, kernel-module operations, and network access are denied by default. Windows and macOS only have the redirect shim; there is no seccomp equivalent there.

Relevant environment variables:

- `AUTHISOLATE` — `enforce` (default), `audit`, or `0` to disable isolation.
- `AUTHISOLATE_NET` — `deny` (default) or `allow`.
- `AUTHISOLATE_LOG` — path to the supervisor log.
- `OSU_AUTH_NO_REEXEC=1` — do not re-execute with the shim on Linux/macOS (useful when debugging).
- `OSU_AUTHREDIRECT` — override the shim path.
- `OSU_AUTH_TRANSPLANT` — override the transplant assembly path.

### `native` (not usable here)

`OSU_AUTH_METHOD=native` (also `reimpl` or `cleanroom`) selects a simplified clean-room implementation. It is a scaffold, not a working signer, and cannot authenticate a client in this repository. Active work on the clean-room implementation happens in a private repository and will not be merged here while `transplant` is the supported mode.

If transplant setup fails, the current code falls back to the native path, which does not currently authenticate. Check the log to see which path was used.

## Running from source

### Requirements

- .NET SDK 10.0 (see `global.json`).
- Git.
- A C compiler for the auth redirect shim:
  - Linux: `cc` (`build-essential` or `base-devel`) and libseccomp (`libseccomp2` on Debian/Ubuntu, `libseccomp` on Arch).
  - macOS: Xcode Command Line Tools (`clang`).
  - Windows: Visual Studio Build Tools with the C++ workload, or mingw-w64. Without a compiler the shim is not built and transplant authentication will not work.
- The usual osu!lazer desktop runtime dependencies (SDL3, OpenGL/Vulkan, audio).

Clone and restore:

```shell
git clone ssh://forgejo@git.ruject.fun/RuJect/osu-oss.git
cd osu-oss
dotnet restore
```

Build and run:

```shell
dotnet run --project osu.Desktop -c Release
```

Use `-c Debug` while working on the code.

### Platform notes

**Linux.** The shim is built automatically as `authredirect.so`. If you use Nix, `nix develop` provides the SDK, compiler, and runtime libraries; `run-osu` starts the client.

**Windows.** Run from a Developer PowerShell where `cl` is available, or install mingw-w64 and pass `-p:AuthRedirectShimWindowsToolchain=mingw` to `dotnet run`. MSVC builds the shim automatically when the Visual Studio environment is present.

**macOS.** Xcode Command Line Tools provide `clang`, which builds `authredirect.dylib` automatically.

### First launch

On first launch, osu!oss downloads the official authentication files. Wait for the notification, then restart. The log should contain `Using transplanted (proprietary) auth signing.` If it says that signing is disabled or that the native path was used, check the shim and the `auth-blobs` cache.

Files specific to osu!oss live next to the normal osu! data:

- `osu-oss/config.ini`
- `osu-oss/themes/`
- `osu-oss/scripts/`
- `osu-oss/auth-blobs/`

## Roadmap

- Prebuilt executables and installers.
- Plugin subsystem.
- Colored usernames.
- Android support; iOS is being considered.
- Sharing skins and settings.
- Custom toolbar widgets.
- Keyboard shortcuts for commands.

No dates. The order may change.

## Contact

- Email: `rus07tam+contact@ruject.fun` (also for legal questions)
- Telegram: <https://t.me/rus07tam_vf>

For security issues, use email rather than a public issue.

## Licence and credits

osu!oss is based on [ppy/osu](https://github.com/ppy/osu) and keeps its MIT licence; see [LICENCE](LICENCE). The osu! and ppy names and branding are not covered by the MIT licence and belong to ppy Pty Ltd. Game resources and the downloaded official authentication files keep their original terms.
