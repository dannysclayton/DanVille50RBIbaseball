# Release packaging

Packaging is intentionally split into two release channels. Do not use one channel's profile, artifact directory, installer definition, or version properties for the other channel.

| Channel | Publish script | Publish profile | Installer definition | Version | Default output |
| --- | --- | --- | --- | --- | --- |
| Public | `publish-public.ps1` | `PublicV1SingleFile` | `packaging/installer/PublicV1.iss` | 1.0.0.0 | `artifacts/public-v1.0` |
| Local only | `publish-local-v2.ps1` | `LocalV2SingleFile` | `packaging/installer/LocalV2.iss` | 2.0.0.0 | `artifacts/local-v2.0` |

The Public build rejects packaged audio, image, and video assets. The Local-only build requires every source asset to be present and byte-identical. Both scripts clean only their own directory below `artifacts`, perform a locked restore, publish without an implicit restore, validate version/content, and create `SHA256SUMS.txt`.

## Prerequisites

- .NET SDK `8.0.422`, pinned exactly by `global.json`.
- Inno Setup 6 only when `-BuildInstaller` is requested.
- Windows SDK SignTool plus an Authenticode code-signing certificate only when `-Sign` is requested.

The scripts locate the pinned SDK through `DOTNET_ROOT`, `$HOME/.dotnet`, or `PATH`. To install it without administrator access:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile "$env:TEMP\dotnet-install.ps1"
& "$env:TEMP\dotnet-install.ps1" -Version 8.0.422 -InstallDir "$HOME\.dotnet"
```

## Locked restore

`NuGet.config` clears inherited feeds and maps all packages to NuGet.org. Packaging restores use `--locked-mode`; a dependency change therefore fails until the lock file is deliberately regenerated and reviewed.

Regenerate the application lock after an intentional dependency or SDK change:

```powershell
& "$HOME\.dotnet\dotnet.exe" restore .\StandaloneBaseball\StandaloneBaseball.csproj `
  --configfile .\NuGet.config --runtime win-x64 `
  -p:RestorePackagesWithLockFile=true -p:RestoreLockedMode=false
```

Regenerate the test-project lock without editing the test project:

```powershell
& "$HOME\.dotnet\dotnet.exe" restore .\StandaloneBaseball.Tests\StandaloneBaseball.Tests.csproj `
  --configfile .\NuGet.config `
  -p:RestorePackagesWithLockFile=true -p:RestoreLockedMode=false
```

Review every `packages.lock.json` change before accepting it. Routine verification must use `--locked-mode`.

## Publish commands

Publish the sanitized Public Version 1.0 application:

```powershell
.\publish-public.ps1
```

Publish the complete Local-only Version 2.0 application:

```powershell
.\publish-local-v2.ps1
```

Build each channel's separate installer after its publish checks pass:

```powershell
.\publish-public.ps1 -BuildInstaller
.\publish-local-v2.ps1 -BuildInstaller
```

Installer output is written below `artifacts/installers`. Public and Local-only installers use different application IDs and installation directories, so one cannot upgrade or overwrite the other accidentally.

## Authenticode signing

Signing is opt-in. Without `-Sign`, no signing credential or SignTool installation is required. With `-Sign`, configuration and certificate credentials are validated before publish output is cleaned.

For a certificate in the current user's `My` store:

```powershell
$env:DANS_RBI_SIGNING_CERTIFICATE_THUMBPRINT = "CERTIFICATE_THUMBPRINT"
.\publish-public.ps1 -BuildInstaller -Sign
```

For a PFX file:

```powershell
$env:DANS_RBI_SIGNING_CERTIFICATE_PATH = "C:\secure\codesigning.pfx"
$env:DANS_RBI_SIGNING_CERTIFICATE_PASSWORD = "PFX_PASSWORD"
.\publish-local-v2.ps1 -BuildInstaller -Sign
```

`packaging/signing/signing.psd1` contains non-secret defaults and environment-variable names. It also supports a fixed thumbprint/PFX path and an explicit `SignToolPath`, but passwords must remain in the environment. Signing fails with a specific error when requested without a certificate, PFX password, SignTool, timestamp URL, or valid signature verification.

## Application icon

`StandaloneBaseball/Branding/DansRBIBaseball.ico` is embedded into the application and used by both installers. It contains 16, 24, 32, 48, 64, 128, and 256 pixel frames derived from the approved game logo. The icon is embedded in the Public executable; the source PNG is not copied into the Public package.
