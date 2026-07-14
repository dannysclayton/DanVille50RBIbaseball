# Dan's RBI Baseball 2026

Dan's RBI Baseball 2026 is a standalone Windows baseball game, editor, simulator, and dynasty manager built from the original RBI Baseball concept but no longer tied to NES ROM limits.

Public release: Version 1.0. Local-only full-media release: Version 2.0. Both builds produce `DanVille50RBIbaseball.exe` in separate output folders.

The project now supports full league creation, team editing, playable games, watchable CPU games, live simulation, dynasty history, custom assets, rankings, playoffs, awards, Hall of Fame tracking, records, cutscenes, music, and exportable pages.

For the complete feature inventory, see [Feautures.md](Feautures.md).

## Core Areas

- Standalone league saves using readable `.dbaseball.json` files.
- Portable team assets stored beside each league save in `[league].assets`.
- Launch, loading, menu, lineup, national anthem, gameplay, postgame, and championship presentation screens.
- Full team editor with logos, photos, colors, music, fields, coaches, rosters, uniforms, cutscenes, badges, and exported pages, including team-specific or league-wide lineup cards with embedded team logos.
- Saved `Assets\Data\schools.csv` source file for school-based team creation, with a menu option to update it from newer CSV versions.
- 30-player varsity rosters with JV pool support, redshirts, medical tags, injured reserve, player avatars, sprites, uniforms, classifications, and progression.
- Shared lineup, rotation, pitching, strategy, fielding, baserunning, injury, and game-resolution systems for user games and simulations.
- Playable keyboard/controller games, Player vs CPU, Player vs Player, CPU vs CPU watch mode, and live season simulation.
- Configurable dynasty setup with game length, mercy rule, extra-inning rule, schedule structure, user-controlled teams, and named dynasties.
- User full name is stored with the dynasty and used in the default save filename.
- Conference, Region, and District structure with rankings, playoff qualification, seeding, series formats, championship handling, and badges.
- Season, career, playoff, all-star, hierarchy, team, player, coach, Hall of Fame, and records tracking, including complete batting, pitching, fielding, catcher, and availability statistics.
- A per-user Replay Library imports replay JSON into application-managed storage, lists playback quality, and supports watch, remove, and open-folder actions without developer-machine paths.
- A Season Game Library stores native Word lineup and full game-result forms under both participating teams, including substitutions, position changes, box scores, decisions, player statistics, and inning-by-inning play-by-play. Forms can be saved for one team, selected teams, or league-wide.

## Game Structure

Dynasties use a Conference -> Region -> District model. Conferences are user-configurable, regions have minimum structure rules, and districts must be even-numbered.

Schedules are generated when the dynasty is created. Users can set district, region, conference, and non-conference home/away counts, plus series length. Schedules are grouped by week and played in order. Users can play, watch, simulate a game, or simulate larger schedule blocks.

The first scheduled game of every season displays the packaged "Baseball Is Back" image for three seconds and then the DanVille 50 logo for three seconds before normal pregame loading. Game 2 of a scheduled doubleheader displays the "Baseball Is Back" image for five seconds before that game's normal pregame sequence.

Playoffs include district champions, runners-up, and wild cards. Round names scale from Bi-District and Area through Regional, Conference, Semi-Finals, and World Series depending on bracket size.

## Gameplay

The game engine resolves plate appearances and live play using player ratings, pitch arsenal, pitch location, swing type, timing, fatigue, fielding, baserunning, strategies, coach quality, and game context.

Supported systems include:

- Pitch types, pitch effectiveness, and batter pitch strengths/weaknesses.
- Starter and reliever fatigue rules.
- Pitching rotations and bullpen roles.
- Steals, double steals, tag-ups, extra-base attempts, and contested throws.
- Bunts, sacrifice hits, sacrifice flies, hit and run, defensive alignments, intentional walks, pitchouts, pickoffs, and mound visits.
- Errors, wild pitches, passed balls, balks, double plays, hit batters, and injury risk.

Live games can be saved in progress and resumed from the Game tab. The in-game controls also allow switching between CPU vs CPU watch, user control of either team, and sim-to-finish without exiting the game.

Teams can save multiple Home, Home Alternate, Visitor, and Visitor Alternate uniform sets. Player-specific jersey, pants, and cap/helmet overrides still take priority when set.

## Dynasty Systems

Players progress and regress through classification-based seasons. Freshmen, Sophomores, Juniors, and Seniors develop through offseason progression, performance boosts, potential, work ethic, durability, and risk. Seniors graduate unless redshirt or medical rules extend eligibility.

The dynasty layer also includes:

- A dynasty history archive stored inside each league save, including season schedules, results, playoffs, rankings, awards, all-star selections, champions, and offseason state.
- All-Star selections and All-Star Game.
- Position awards and named awards.
- Winner trophy galleries on team, player, Awards, and Hall of Fame pages, with original or generated plaque selection and image export.
- Coach records and coach Hall of Fame.
- Player Hall of Fame with regular-season, playoff, all-star, and leader bonuses.
- Rankings with preseason, weekly, and final polls.
- Inbox-style messages for game results, statistics, player of the game, and scouting reports.
- Records books at league, conference, region, district, and team levels.

## Assets

The app has two asset layers:

- App assets: stored in the project `Assets` folder and copied during build.
- League/team assets: stored beside the league save and used for publishable custom leagues.

Team assets include logos, photos, player avatars, sprites, music, National Anthem images, cutscenes, badge templates, generated badges, base lineups, and pitching plans.

Fresh installations do not assume an external asset-library location. During dynasty creation or later in Settings, a user may choose an existing library or create a new local library with Audio, Images, Video, and Teams folders. Imported assets from that library or other folders are copied into the correct team asset folder when assigned, so published leagues do not depend on the original source library.

Packaged app assets are read-only seeds. Editable global data is stored under `%LOCALAPPDATA%\DanVille50\Dan's RBI Baseball 2026`: `Data\schools.csv`, league cutscenes, the shared team-music playlist, and temporary assets for unsaved teams. On first use, packaged seed content is copied into the user location without overwriting later user changes. This applies to both public Version 1.0 and local-only Version 2.0 and allows installation under Program Files.

Unhandled errors are contained separately by distribution channel. Public Version 1.0 logs to `%LOCALAPPDATA%\DanVille50\Dan's RBI Baseball 2026\Logs\Public`; Local-only Version 2.0 logs to `Logs\LocalV2`. Each version installs its own WinForms UI exception handler, AppDomain fatal handler, unobserved-task handler, startup boundary, and recovery/exit dialog before the launch form is created.

Replay files are imported explicitly from the Replays menu into `%LOCALAPPDATA%\DanVille50\Dan's RBI Baseball 2026\Replays`. The Replay Library never reads from a developer desktop path. `Save Template...` gives the user an editable schema-v2 `.rbi-replay.json` example and the complete `ExactReplaySchema.md` format guide; edited files can then be imported as Exact, Best Effort, or Snapshot replays.

## Build

Requirements:

- .NET 8 SDK
- Windows desktop runtime support

Build from the project folder:

```powershell
dotnet build .\StandaloneBaseball.csproj -c Debug
```

Release build:

```powershell
dotnet build .\StandaloneBaseball.csproj -c Release
```

Create the public self-contained Win64 Version 1.0 release:

```powershell
.\publish-public.ps1
```

The public release excludes audio and image/video media without stored redistribution rights. It uses code-rendered visual fallbacks and silence, writes to `artifacts\public-v1.0`, and fails if uncleared media enters the package.

Create the private Version 2.0 build for this computer with every current asset unchanged:

```powershell
.\publish-local-v2.ps1
```

This writes to `artifacts\local-v2.0`, verifies all source assets are present, verifies each published asset against the source SHA-256 hash, and verifies executable version `2.0.0.0`. Do not publicly redistribute this package unless the media rights documented in `MEDIA_LICENSE_RECOMMENDATIONS.md` are resolved.

## Tests And Coverage

Run the complete regression suite from the solution folder:

```powershell
dotnet test .\DansRBIBaseball2026.sln -c Debug
```

Generate and validate the Cobertura coverage report:

```powershell
.\run-coverage.ps1
```

The script fails if the report is missing, contains no instrumented lines, or falls below the regression floors. It writes the report to `TestResults\Coverage\coverage.cobertura.xml`. The July 2026 baseline is 142 passing tests with 42.19% line, 30.35% branch, and 60.23% method coverage.

The suite includes deterministic game-engine and simulation checks, complete in-progress game state round trips, backup recovery, 2/4/8-conference playoff advancement, injury and fatigue rules, rankings, awards, Hall of Fame scoring, and championship lifecycle behavior.

Run:

```powershell
dotnet run --project .\StandaloneBaseball.csproj
```

## Save Format

League files are saved as `.dbaseball.json`. They contain league rules, teams, players, coaches, schedules, seasons, results, rankings, awards, Hall of Fame data, records, inbox messages, field presets, and replay references. Individual committed games autosave immediately; season and playoff batch simulations autosave once after the completed batch.

Assets referenced by the save should resolve from the league asset folder first, then from the app folder.
