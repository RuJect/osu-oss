# osu!oss deploy

Vendored from [ppy/osu-deploy](https://github.com/ppy/osu-deploy) (MIT).

This tool publishes osu!oss builds with Velopack:

- Windows: one-click installer and delta updates.
- Linux: self-updating AppImage.
- macOS: portable `.app` zip.

## Configuration

`App.config` contains the fork defaults:

- repository: `RuJect/osu-oss`
- package id: `org.ruject.osu`
- title: `osu!oss`
- author: `RuJect`
- executable: `osuoss`

Secrets are read from the environment:

- `GITHUB_TOKEN`
- `OSU_DEPLOY_WINDOWS_SIGN_PARAMS`
- `OSU_DEPLOY_AppleCodeSignCertName`
- `OSU_DEPLOY_AppleKeyChainPath`

## Local usage

```shell
dotnet run --project tools/osu-deploy -- "" 2026.1005.0-lazer Linux
```

On Windows, run the deploy tool from a Developer PowerShell so the auth redirect shim can be built.

## Debug signing

Windows:

```powershell
tools/osu-deploy/scripts/create-windows-debug-cert.ps1
```

Then set `OSU_DEPLOY_WINDOWS_SIGN_PARAMS` as printed by the script.

macOS:

```shell
tools/osu-deploy/scripts/create-macos-debug-cert.sh
```

Then set `OSU_DEPLOY_AppleCodeSignCertName` and `OSU_DEPLOY_AppleKeyChainPath` as printed by the script.

These certificates are self-signed. The resulting builds are not trusted by other machines and will trigger SmartScreen or Gatekeeper warnings.
