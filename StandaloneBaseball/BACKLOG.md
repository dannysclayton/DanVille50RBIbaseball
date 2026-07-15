# Dan's RBI Baseball 2026 - Improvement Backlog

This backlog was audited against the implementation and available tests on July 13, 2026. Completed foundations are recorded below so the remaining work is limited to concrete behavior, UI, and regression gaps.

## Task 5 - Harden pitcher decisions (completed July 2026)

Completed:

- Played and CPU-watched games assign wins, losses, saves, and holds; simulated games populate the same `GameResult` pitcher-of-record fields and player stat fields.
- Both engines update winning and losing candidates when the lead changes and charge the go-ahead runner to the responsible pitcher instead of selecting the pitcher with the most outs.
- Playable-game save/resume persists pitcher-decision candidates and relief appearance state.
- `PitcherDecisionEngine` is the single decision source for playable, watched, and detailed simulated games.
- Starters need 15 outs for a win in scheduled 6-9 inning games and 12 outs in scheduled 5-inning games.
- The loss follows the pitcher responsible for the permanent go-ahead runner, including a pitcher removed before recording an out.
- Scorer discretion bypasses a brief, ineffective winning reliever for the most effective eligible reliever.
- All save paths, holds, blown saves, complete games, and shutouts are assigned by the shared engine.
- Save/resume preserves three-run-lead and tying-run-threat entry state so resumed games retain identical decision eligibility.
- Deterministic regression scenarios cover starter eligibility, scorer discretion, all save paths, holds, blown-save-plus-win outcomes, inherited-run losses, complete games, shutouts, and state round trips.

## Task 6 - Finish contextual injury coverage (completed July 2026)

Completed:

- Injury recovery and rolls run before individual played/CPU-watched games, individual simulations, full-season simulations, and simulated playoff games.
- Day-to-day players remain available with a 10% effective-rating penalty used by shared gameplay calculations.
- Hit-by-pitch injuries and pitcher-overwork injuries are implemented in both playable and detailed simulated games; rotation size affects pitcher injury risk.
- Normal in-game injuries are participant-based in both gameplay paths: pitchers are exposed per pitch, batters per plate appearance, fielders when involved in a play, runners while advancing or stealing/sliding, and catchers per completed defensive half-inning.
- Close contested plays, double-play slides, and close steal attempts use a separate higher-risk collision exposure. Bench players do not receive normal in-game injury rolls until they enter and participate.
- Rare pregame illness/warmup incidents remain possible at one or two chances per thousand based on durability, independent of rotation size.
- Pitcher exposure includes three-man/four-man rotation risk and cumulative consecutive-relief-use risk. Playable pitch counts now increment for every pitcher, not only the privately tracked starter counter.
- Unavailable-player and injured-reserve missed games are persisted in player game lines and season/career aggregations, with deterministic engine regression tests.

Remaining work is tracked under regression coverage: add deeper mode-level scenarios proving medical-tag missed-game accounting occurs exactly once for played, watched, season-simulated, and playoff games.

## Task 7 - Complete exact replay playback controls (partially completed July 2026)

Completed:

- Schema-v2 exact replays render through an embedded `GameplayForm`, applying recorded game state, fielders, ball and runner interpolation, scoring, substitutions/state changes, audio, cutscenes, and scoreboard snapshots.
- Playback supports play/pause, reset, event step, timed event transitions, deterministic validation, and exact final-state checks.
- Playback-speed selection and previous/next event and inning navigation rebuild deterministic state correctly when seeking backward.
- Best-effort loading reconstructs missing states and timing, tolerates missing optional audio/cutscene assets, and preserves legacy snapshot replay compatibility; these fallback paths have regression coverage.

Remaining:

- Add an end-to-end visual playback regression that verifies exact frames reach the gameplay rendering surface, not only the replay engine/state layer.

## Task 8 - Complete the postgame box score (partially completed July 2026)

Completed:

- `GameResult` stores inning runs, hits, errors, left on base, game length, stadium, rules, mode, game type, uniforms, playoff context, play-by-play, pitcher decisions, and full player game lines.
- Played and detailed simulated games populate those fields, and committed results drive records-book/statistical views.
- The postgame dialog displays an inning line score, R/H/E/LOB and additional team totals, projected records for both teams, and the winner's projected updated record.
- The postgame dialog includes sortable, read-only batting and pitching tables for both teams with lineup/starter ordering and empty-data handling.

Remaining:

- Add persistence/round-trip regression coverage specifically for all `GameResult` metadata and pitcher-of-record fields.

## Task 9 - Complete MLB-level statistics (completed July 2026)

- Added PA, XBH, reached on error, holds, complete games, shutouts, runs allowed, doubles/triples allowed, defensive innings, total chances, and catcher caught-stealing percentage.
- Played and simulated games now record the raw events; season, career, playoff, all-time, hierarchy, records-book, Hall of Fame, team Hall, and All-Star aggregations preserve them.
- Team, player, hierarchy, All-Star, and Hall of Fame screens expose the fields, with sortable/exportable grids and rate-qualified catcher leader support.
- Added deterministic persistence, formula, and simulated-game invariant regression coverage.

## Task 10 - Enhance native Office exports (partially completed July 2026)

Completed:

- Excel and Word actions now create genuine Open XML `.xlsx` and `.docx` packages instead of HTML files with Office extensions.
- Exports preserve the current grid row order and headings, use styled table headers, and give Word exports a landscape page layout.
- Excel exports preserve typed numeric, Boolean, and date/time cells; strings remain inline text.
- Tests parse generated Open XML, verify package parts, and assert cell references, types, values, ordering, and styles.

Remaining:

- Add report-specific team colors and embedded logos where those assets are available.
- Validate generated files in an Office-compatible desktop application in addition to the automated Open XML checks.

## Task 11 - Finish save lifecycle protection (partially completed July 2026)

Completed:

- Dynasty files carry save schema version 2, legacy/unversioned saves migrate through ordered version steps, and future unsupported versions are rejected clearly.
- Saves use a durable temporary file followed by atomic replacement, retain 12 rotating backups, validate recovery candidates, and provide automatic-on-open and manual recovery dialogs.
- Save failures preserve in-memory dirty state, and in-progress games can be saved and resumed with regression coverage.
- Individual committed games autosave immediately; new unsaved dynasties request their first save location, and season/playoff batch simulations autosave once after the committed batch.
- Roster changes, schedule generation, award finalization, and Hall of Fame changes use a coalesced high-value autosave after an initial save path exists.
- Existing assets are normalized to portable dynasty-relative references when possible; user-editable global data uses per-user local application data.

Remaining:

- Add UI-level recovery tests for selecting a backup and saving the recovered dynasty over a damaged primary file.

## Task 12 - Stabilize expanded regression coverage (partially completed July 2026)

Completed:

- Expanded coverage includes lineups, schedules, shared game resolution, deterministic simulations, complete live-game save state, atomic saves, backup recovery, Program Files-safe user data, separate public/local crash logging, portable asset and replay-library defaults/imports, replay templates, release packaging metadata, playoff advancement, injuries, pitcher fatigue, rankings, awards, Hall of Fame rules, championship persistence, custom scoreboard rendering/replay fallback, and WinForms workflows.
- Added complete bracket scenarios for 2, 4, and 8 conferences; the 8-conference test found and fixed an early national-round merge that could create multiple World Series matchups.
- Moved championship persistence and post-World-Series injury reset into `ChampionshipLifecycleEngine` so the full transition is testable without the editor UI.
- Added `run-coverage.ps1` and `coverage.runsettings`. The verified July 2026 baseline is 45.85% line, 33.18% branch, and 63.24% method coverage for `StandaloneBaseball`; the script resolves the pinned SDK itself and fails below 38% line, 27% branch, or 55% method coverage.
- Added deterministic WinForms coverage for launch, menu/controller navigation, dynasty setup, cutscene and field editors, live simulation, championship dialogs, and representative `MainForm` workflows.
- The complete suite passes under the pinned .NET 8 SDK: 190 passed, 0 failed.
- Nullable reference analysis is enabled. Possible-null dereferences, unsafe null arguments/literals, and nullable-value dereferences (`CS8602`, `CS8604`, `CS8625`, and `CS8629`) are build errors in both release channels.
- The no-incremental solution build completes with zero nullable warnings and zero build warnings overall.

Remaining:

- Add deeper behavioral tests for long interactive game sessions and pixel-level rendering; the current automated UI tests emphasize deterministic workflow/state behavior.

## Additional Verification Task - Playoff bracket scenarios (partially completed July 2026)

- Automated full advancement now covers 2, 4, and 8 conference structures through one World Series winner, including hierarchy isolation, final-round naming, series length, and championship persistence.
- Existing tests cover qualification-based home advantage, seeded home-field rotation, round naming, district/region/conference feeder links, and wildcard/champion priority.
- Badge rendering and full coach-record updates remain integration-level UI verification items.
