# Dan's RBI Baseball 2026

Dan's RBI Baseball 2026 is a standalone Windows baseball game with season and dynasty modes, and team editor. This repository contains the public Version 1.0 source, regression tests, reproducible publishing configuration, and a self-contained Windows build.

## Public Release

The current self-contained Windows executable is available at [`release/public-v1.0/DanVille50RBIbaseball.exe`](release/public-v1.0/DanVille50RBIbaseball.exe). Its SHA-256 checksum is recorded in [`release/public-v1.0/SHA256SUMS.txt`](release/public-v1.0/SHA256SUMS.txt).

The public package intentionally uses code-rendered fallback visuals and silence where redistribution rights for local media have not been established. You will be able to use your own music, whether in game, menu, or even when viewing statistics. In addition, photos and video can be added to create "Cut Scenes" and also be displayed in game. 

## Features

The complete feature inventory is in [`StandaloneBaseball/Feautures.md`](StandaloneBaseball/Feautures.md). Major systems include:

- Playable, watched CPU-vs-CPU, and detailed simulated games using shared rules.
- Dynasty creation, scheduling, playoffs, rankings, awards, records, and Hall of Fame tracking.
- Team, roster, uniform, stadium, scoreboard, sprite, music, and cutscene editors.
- Keyboard and XInput controller support with selectable team assignments.
- In-progress game saves, replay import/playback, portable user data, and dynasty backup recovery.
- Complete team/player statistics with sortable views and native Excel/Word export.
- complete editable features so the user can make any league, team, district...anything you desire! 

## Build

Requirements:

- Windows 10 or Windows 11 x64
- .NET SDK `8.0.422`, pinned by [`global.json`](global.json)

Run the tests:

```powershell
dotnet restore .\DansRBIBaseball2026.sln --configfile .\NuGet.config --locked-mode
dotnet test .\DansRBIBaseball2026.sln -c Release --no-restore
```

Build and audit Public Version 1.0:

```powershell
.\publish-public.ps1
```

The publish script performs a locked restore, produces the self-contained executable, verifies version metadata and required files, rejects uncleared packaged media, and writes SHA-256 checksums.

## Media And Attribution

See [`StandaloneBaseball/THIRD_PARTY_ATTRIBUTIONS.md`](StandaloneBaseball/THIRD_PARTY_ATTRIBUTIONS.md) and [`StandaloneBaseball/MEDIA_LICENSE_RECOMMENDATIONS.md`](StandaloneBaseball/MEDIA_LICENSE_RECOMMENDATIONS.md). Attribution does not itself grant redistribution rights.
