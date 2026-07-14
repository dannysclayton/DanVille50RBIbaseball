# Dan's RBI Baseball 2026 - Feature Catalog

This file catalogs the current feature set in the standalone Dan's RBI Baseball 2026 project.

## Standalone Game Architecture

- Standalone Windows Forms baseball game/editor built on .NET 8.
- Version 1.0 release metadata with `DanVille50RBIbaseball.exe` as the application executable.
- No runtime dependency on the original NES ROM.
- League saves use readable `.dbaseball.json` files.
- Committed games autosave immediately, while season and playoff batch simulations autosave after the batch; a new dynasty requests its first save location when needed.
- League-specific assets are stored beside the save in a portable `[league name].assets` folder.
- Asset paths are normalized so imported team files can travel with the league instead of pointing back to the original import library.
- App-level assets are copied from the `Assets` folder during build.
- Existing ROM snapshots can be imported as a starting point through the ROM snapshot importer.

## Launch, Loading, and Menu Flow

- Custom launch screen with the Dan's RBI Baseball 2026 logo.
- Launch screen start button.
- Launch screen music and start-click sound support.
- Loading transition screen with custom image, loading bar, and music.
- Main menu screen using the supplied menu art.
- Menu navigation for Start Dynasty, Continue Dynasty, Game, Teams, Seasons, Replays, and Settings.
- Menu page music and button click sound effects.
- "Created With Codex" launch-screen watermark.

## League and Dynasty Setup

- Dynasty naming during creation.
- User full name can be entered during dynasty creation.
- The user's full name is stored in the league save and used in the default dynasty save filename.
- User-selectable game length from 5 to 9 innings.
- User-selectable mercy rule behavior.
- User-selectable extra-inning runner rule.
- Configurable season structure using Conference, Region, and District.
- Supports any number of conferences.
- Requires at least two regions per conference.
- Districts must be even-numbered.
- User-controlled teams can be selected at dynasty creation.
- Users can also choose a controlled team from the schedule when starting a specific game.
- Dynasty history stores completed seasons, champions, awards, stats, rankings, and all-star results.
- Dynasty history acts as the season archive for schedules, game results, playoff series, rankings, awards, all-star selections, champion team ids, pitcher usage, and offseason status.

## Team Management

- Full team editor.
- Team names and mascots support up to 15 characters each.
- Scoreboard abbreviation supports up to 6 characters.
- Full color palette support with manual hex-code entry.
- Primary, secondary, accent, and uniform color use throughout team UI and screens.
- Team logos can be uploaded and saved to the team asset folder.
- Team photos can be uploaded and used on team pages and championship screens.
- Team-specific National Anthem image folder.
- Team-specific music playlist support.
- Team music can be imported from the shared library and copied into the team's own asset folder.
- Team pages support photos, logos, rosters, base lineup, pitching plan, coaches, field selection, music, cutscenes, badges, uniforms, and exports.
- Team pages include a uniform library editor for Home, Home Alternate, Visitor, and Visitor Alternate uniforms.
- Each uniform category can store multiple saved uniform sets for future use.
- Uniform sets include a name, jersey color, pants color, cap/helmet color, optional uniform image, and active selection.
- Teams can be created from the saved `Assets\Data\schools.csv` source file with school colors and logo data when available.
- The saved `Assets\Data\schools.csv` file can be updated from a newer CSV through the Teams menu.
- External roster spreadsheets can be imported when available.
- Extra imported players beyond varsity roster limits are stored in a JV pool.

## Team Assets

- Team assets are stored under the league assets folder.
- Team asset folders include photos, logos, sprites, player avatars, music, National Anthem images, cutscenes, badges, and badge templates.
- Team cutscene folders are organized under `cutscenes`.
- Team cutscene uniform subfolders are `HOME`, `HOME ALTERNATE`, `VISITOR`, and `VISITOR ALTERNATE`.
- Imported team assets are saved as relative paths when possible.
- Publishing-friendly path resolution checks the league asset folder first and the app asset folder second.
- Self-contained single-file publishing keeps every runtime asset in the published `Assets` tree beside the executable, with an audited Win64 publish profile and `publish-single-file.ps1` verification command.
- Two distribution channels: public Version 1.0 excludes uncleared media and uses code-rendered/silent fallbacks; local-only Version 2.0 retains all current assets unchanged and verifies them by SHA-256.
- Program Files-safe editable global storage under the current user's LocalApplicationData, with one-time read-only seeds for schools.csv, league cutscenes, and the shared team-music playlist.
- Separate Public 1.0 and Local 2.0 unhandled-exception boundaries with channel-specific LocalApplicationData logs, recover-or-exit UI dialogs, startup protection, AppDomain logging, and unobserved-task handling.

## Rosters and Players

- Varsity rosters support 30 players.
- Teams require a minimum of 12 pitchers.
- Players can have multiple positions.
- Pitchers can also have non-pitching positions such as DH, 1B, or other fielding positions.
- Position players can have combinations such as 1B/3B, 2B/SS, C/1B, C/3B, and outfield roles.
- Player positions are randomly generated on roster creation and editable by the user.
- Player photos can be uploaded as player-page avatars.
- Player sprite sheets can be generated and assigned.
- Player uniform parts can be edited, including jersey, pants, cap, and batting helmet.
- Player uniform overrides remain available and take priority over team uniform-library defaults.
- Player bats/throws side is tracked.
- Speed, baserunning, stealing, fielding, throwing, catching, and pitching ratings are tracked.
- Hit by pitch is tracked for batters.
- Hit batters are tracked for pitchers.

## Player Classifications

- Players use high school/college-style classifications: Freshman, Sophomore, Junior, and Senior.
- Classification is assigned before ratings are generated.
- Sophomores receive a 5 percent rating reduction from the sophomore/junior baseline.
- Freshmen receive a 10 percent reduction.
- Juniors receive a 5 percent boost.
- Seniors receive a 10 percent boost.
- Classification is editable in the team folder.
- Seniors graduate after the season unless a medical or redshirt repeat applies.

## Player Progression and Regression

- Players have potential, work ethic, durability, and regression-risk fields.
- Offseason progression applies after each completed season.
- Performance-based progression adds extra development based on play.
- Higher-potential players are more likely to receive larger progression boosts.
- Regression can occur as part of the offseason model.
- Redshirt and medical repeat seasons can extend a player career to five years.
- Early Hall of Fame career scoring accounts for players in the first three seasons who could not play a full career.

## Redshirt, Medical, JV, and Injured Reserve

- Teams can redshirt up to five players.
- Redshirted players are ineligible for that season.
- Redshirted players receive a random 1 to 10 percent rating boost weighted by potential.
- Redshirted players repeat their classification year.
- A player can only redshirt once per career.
- Players missing more than the configured injury threshold can receive a medical tag and repeat the classification year.
- Players injured more than 20 games can be moved to Injured Reserve.
- Injured Reserve removes the player from the active roster and marks medical eligibility.
- Teams can call up a JV player to replace an injured player.
- A JV call-up uses that season as part of the player's career.
- Called-up players remain eligible for a future redshirt if otherwise qualified.

## Injuries

- Injury system supports healthy, day-to-day, and out statuses.
- Minor, moderate, and major injury severities are supported.
- Injuries can be generated from normal gameplay.
- Ordinary in-game injury risk applies only to participating players; inactive bench players are not exposed until they enter a game.
- Pitchers receive exposure per pitch, batters per plate appearance, involved fielders per defensive play, runners while advancing or stealing/sliding, and catchers per completed defensive half-inning.
- Close contested bases, double-play slides, and close steal plays have a separate higher collision-injury risk.
- Rare pregame illness or warmup injuries use a durability-based one-or-two-per-thousand chance and are separate from gameplay participation.
- Hit-by-pitch injuries are supported.
- Pitcher overwork injuries are supported.
- Pitcher injury exposure includes three-man/four-man rotation modifiers and cumulative consecutive-relief-game workload.
- Day-to-day players can play with penalties.
- Injury games missed are tracked.
- Injury effects integrate with lineup eligibility.
- Injury effects integrate with player development, medical tags, and Injured Reserve.
- All-Star Game eligibility resets injuries after the World Series because the All-Star Game is played before the offseason.

## Lineups

- Shared lineup engine is used by CPU and user-facing lineup creation.
- Redshirts are assigned before base lineup creation.
- Injured, out, and redshirt players are excluded from normal lineup generation.
- Lineup engine selects nine field-valid starters.
- Mandatory fielding positions must be filled.
- The DH can replace the pitcher in the batting order.
- Batting order is role-based:
  - Leadoff emphasizes contact, speed, and on-base skill.
  - Second hitter emphasizes contact, speed, on-base skill, and some power.
  - Third hitter emphasizes contact, power, and RBI ability.
  - Cleanup emphasizes power and RBI ability.
  - Fifth emphasizes power and RBI ability.
  - Sixth through ninth balance contact, speed, and power.
- Base lineups are saved to each team folder and are editable.
- Lineups only change automatically for injuries, redshirts, roster changes, or user edits.
- All-Star teams use the same lineup system.

## Defensive Position Flexibility

- If no eligible player is available for a position, a similar-position fallback can be used.
- No penalty applies for 1B/3B, 2B/SS, 2B/3B, outfield-to-outfield, or pitcher-to-pitcher substitutions.
- Unqualified position use applies a 25 percent fielding penalty.
- Unqualified position use adds 10 percent injury risk.
- If a player plays 10 consecutive games at an unqualified position and has an open secondary or third position slot, that position is added as a qualified position.

## Pitching Rotations and Bullpen Roles

- Each team has an editable pitching rotation file/page.
- Supports 3-man, 4-man, and 5-man rotations.
- 3-man rotations apply a 10 percent max pitch-count penalty and 5 percent added injury risk.
- 4-man rotations apply a 5 percent max pitch-count penalty and 3 percent added injury risk.
- 5-man rotations have no penalty.
- Starter rest tracking is enforced.
- A scheduled starter cannot pitch in the game immediately before their start.
- A scheduled starter cannot pitch in the game immediately after their start.
- Starters can pitch limited relief in other games.
- Starter relief use is capped at three outs before next-start penalties apply.
- Each extra out beyond three reduces next-start max pitch count by 10 percent.
- Starter relief penalties are cumulative.
- Bullpen roles can be user-assigned or auto-assigned.
- Closer selection favors outs, strikeouts, and overall relief quality.
- Setup pitchers are selected from the next best relief arms.
- Long relief favors stamina and quality.
- Relievers pitching back-to-back games suffer a cumulative 10 percent stat reduction per consecutive game.

## Pitch Arsenal

- Pitchers have editable pitch arsenals.
- Available pitch types are Fastball, Curveball, Slider, Changeup, Splitter, Forkball, and Knuckleball.
- Pitchers are created with at least Fastball plus two additional pitches.
- Pitchers can have up to five total pitches.
- Each pitch has an effectiveness rating.
- Effectiveness scout labels include Exceptional, Above Average, Average, and Poor.
- Exceptional represents the top 10 percent.
- Above Average represents the 70th to 90th percentile.
- Average represents the 40th to 60th percentile.
- Poor represents below the 40th percentile.
- Pitchers use stronger pitches more often for strikeouts and weak contact.
- Emergency position-player pitchers receive three random pitches.
- Emergency pitchers are not required to have a fastball.
- Emergency pitchers cannot receive Exceptional pitch effectiveness.
- Emergency pitcher effectiveness is usually Poor to Average with a smaller chance of Above Average.

## Batter Pitch Matchups

- Batters can have pitch-type strengths and weaknesses.
- Classification affects how many pitch strengths and weaknesses a batter starts with.
- Freshmen can begin with more weaknesses and fewer strengths.
- Seniors can begin with more strengths and fewer weaknesses.
- Pitch type, pitch effectiveness, batter strength/weakness, swing type, timing, and location affect results.

## Pitcher Fatigue

- Pitchers receive a random career pitch-count base when created.
- Classification determines how much of the pitch-count base is available in the current season.
- Seniors can access the full amount.
- Juniors, Sophomores, and Freshmen receive reduced available pitch-count percentages.
- For every 10 innings pitched in a season, a pitcher gains 1 career pitch-count point.
- Pitch-count growth rounds up.
- Starters have max pitch counts based on their available seasonal pitch count.
- Relievers can pitch six outs before reaching their max relief workload.
- After a pitcher reaches the fatigue threshold, base runners allowed in an inning trigger penalties.
- Two base runners after threshold reduces stats by 10 percent.
- Three base runners after threshold reduces stats by 20 percent.
- Four base runners after threshold can turn strikeouts into singles with runners advancing two bases.
- Errors do not count toward these fatigue base-runner thresholds.
- Pitchers allowing five runs in an inning must be removed.
- Pitchers allowing six runs over two innings must be removed.
- Pitchers allowing seven runs over three innings must be removed.
- If no pitcher is available, a position player is used as an emergency pitcher.
- Emergency pitching stats are based on 25 percent of the team's top starter.
- Five earned runs in three or fewer innings reduce pitcher stats by 10 percent.
- Earned-run penalties stack for each additional five earned runs in the same window.
- Consecutive scoreless inning streaks can restore and boost pitching performance.
- Five consecutive scoreless innings gives a 10 percent boost.
- Six, seven, and eight consecutive scoreless innings each add another 10 percent boost.
- At eight scoreless innings, the pitcher is protected from further reduction for the rest of the game.
- Relievers entering mid-inning receive a one-batter same-side matchup boost when applicable.

## Pitcher Decisions

- Playable, watched, and detailed simulated games use one shared pitcher-decision engine.
- A starter must record 15 outs to qualify for a win in scheduled 6-9 inning games.
- A starter must record 12 outs to qualify for a win in a scheduled 5-inning game.
- The loss is charged to the pitcher responsible for the permanent go-ahead runner, even when that pitcher records no out.
- Scorer discretion can bypass a brief, ineffective winning reliever for the most effective eligible reliever.
- Saves support a lead of three runs or fewer with at least one full inning, the tying run on base/at bat/on deck, or at least three effective finishing innings.
- Holds require entering a save situation, recording at least one out, and leaving with the lead intact without receiving the win, loss, or save.
- Blown saves are tracked separately and can occur in the same appearance as a win.
- Complete games and shutouts are assigned when the starter is the team's only pitcher and finishes the game.
- Win, loss, save, hold, blown-save, complete-game, and shutout totals persist to game, season, career, playoff, hierarchy, records-book, and Hall of Fame stat displays.
- Blown saves are displayed and recorded but do not award positive Hall of Fame points.
- In-progress saves retain pitcher-decision candidates and reliever save-entry conditions.

## Coaches

- Teams can add and edit coaches.
- Coaches track wins, losses, playoff wins, championship wins, and Hall of Fame points.
- Coaches have a role, style, and strategy.
- Coach styles include Below Average, Average, Above Average, and Championship.
- Coach strategies include Safe, Conservative, and Aggressive.
- Coach style affects the chance of calling the right strategy.
- Below Average coaches have a 25 percent right-strategy chance.
- Average coaches have a 50 percent right-strategy chance.
- Above Average coaches have a 75 percent right-strategy chance.
- Championship coaches have a 100 percent right-strategy chance.
- Safe coaches avoid gamble calls.
- Conservative coaches prefer safe calls but can gamble when the game is on the line.
- Aggressive coaches try to score or prevent scores regardless of risk.
- Coaches can visit the mound once per inning to settle a pitcher.
- First mound visit can boost the pitcher against the current batter.
- A second mound visit in the same inning requires a pitching change.
- Aggressive coaches are more likely to use starters in relief during close deficit games.

## Game Modes and Controls

- Playable game mode.
- Watchable CPU vs CPU mode.
- Simulated live game mode.
- Player vs CPU mode.
- Player vs Player mode.
- Users can choose which team they control.
- Users can change live game input mode in-game between CPU vs CPU watch, user controlling the away team, user controlling the home team, and sim-to-finish.
- In-progress games can be saved from inside the live game window.
- Saved in-progress games can be resumed from the Game tab.
- Keyboard input support.
- XInput controller support.
- Game actions can be mapped through keyboard/controller-style commands.
- Pitching, batting, baserunning, throwing, strategies, mound visits, and pitcher changes are command-driven.

## Shared Game Engine

- Playable games and simulated games use the same rating-based game logic path.
- Game outcomes factor pitch type, pitch location, swing type, swing timing, batter ratings, pitcher ratings, fielding ratings, baserunning ratings, coach strategy, and defensive alignment.
- Live simulation presents game events as they happen.
- Play-by-play narration is tracked.
- Box score data is generated from the same game events that update stats.
- Season sim path uses generated games rather than score-first/stat-later shortcuts.

## Game Rules

- User-selectable innings from 5 through 9.
- Mercy rule can end a game when a team leads by at least 10 after the top of the 5th or later configured threshold.
- Extra innings can start with a runner on second.
- Users can pick the extra-inning runner if the runner is not scheduled to bat within the next eight at-bats.
- A runner used as an extra-inning runner is not removed from the game unless used a second time as a pinch runner or hitter.
- Courtesy runner rules protect catchers and pitchers on base.
- Pitchers and catchers are not removed from the game when a courtesy runner is used.
- Automatic intentional walks are supported.
- Automatic IBB does not add pitches to the pitch count.
- IBB is tracked separately from normal walks.
- Hit by pitch, walks, sacrifice hits, sacrifice flies, bunts, stolen bases, caught stealing, wild pitches, passed balls, balks, errors, double plays, and game-ending outs are tracked.

## Offensive Strategy

- Hit and Run.
- Steal.
- Safe.
- Bunt.
- Sacrifice bunt.
- Double steal.
- Strategy availability is context-sensitive.
- Unavailable strategies are greyed out in the strategy UI.
- Hit and Run can advance runners from first, second, and third depending on the situation.
- CPU strategy selection considers score, inning, runners, outs, coach quality, and coach strategy.
- CPU can call sacrifice plays in logical late-game or run-needed situations.

## Defensive Strategy

- Normal.
- Infield In.
- Double Play.
- Outfield In.
- No Doubles.
- Wheel Play.
- Intentional Walk.
- Defensive strategy availability is context-sensitive.
- CPU defense considers base/out state, score, inning, coach quality, and coach strategy.

## Steal Defense

- Hold Runner.
- Slide Step.
- Pitchout.
- Pickoff.
- Steal outcomes consider runner speed, runner baserunning intelligence, steal aggressiveness, pitcher hold/pickoff/delivery ability, catcher arm/pop time/accuracy, and defensive call.
- Safe sound effects are used on successful steals and contested safe plays.

## Baserunning

- Players have both speed and baserunning intelligence ratings.
- Baserunning intelligence affects reads on balls in play.
- Runners can attempt extra bases.
- Runners can tag up.
- Runners can attempt to score.
- Defensive throws can contest hits and advancement.
- Singles, doubles, triples, home runs, contested reaches, and baserunner advancement are handled through gameplay events.

## Fielding and Errors

- Fielding uses position-relevant defensive ratings.
- Error chance is affected by classification and defensive ability.
- Seniors use the highest classification defensive effectiveness.
- Younger players receive reduced defensive effectiveness.
- Errors reduce a player's defensive rating through penalty debt.
- Error-free putouts and assists can restore lost defensive points.
- Ten straight error-free putouts or assists can restore one point.
- Errors integrate with pitcher fatigue so fatigue base-runner thresholds do not count error-created runners.
- Player errors and team errors are tracked.
- Defensive double plays are tracked.
- Batter grounded into double plays are tracked.
- Passed balls are tracked for catchers.
- Wild pitches and balks are tracked for pitchers.

## Scheduling

- Season schedule is created when a dynasty is created.
- Users configure district, region, conference, and non-conference game counts.
- Users configure home and away counts for each game type.
- Schedules are grouped into weeks and ordered games.
- Teams play aligned schedules by week and day.
- Series length can be configured.
- One-game series play Friday only.
- Two-game series play Friday and Saturday.
- Three-game series use the normal weekend structure.
- Four-game series use Friday, Saturday, and a Sunday doubleheader.
- Larger series add back-end doubleheaders up to six games.
- Users can play, watch, sim one game, or sim larger schedule ranges.

## Playoffs

- Playoff structure follows Conference, Region, and District hierarchy.
- District champions qualify.
- District runners-up qualify.
- Wild cards qualify based on configured rules and records.
- District champions receive first-round byes where applicable.
- Runner-up teams play cross-district wild cards where applicable.
- Additional wild cards can be added to balance bracket size.
- Wild cards are selected using conference record when balance is needed.
- Best wild card can play the lowest wild card to balance the playoff field.
- Round 1 is Bi-District.
- Round 2 is Area.
- Round 3 is Regional Quarter Finals.
- Larger brackets can use Regional Semi-Finals, Regional, Conference Quarter-Finals, Conference Semi-Finals, Semi-Finals, and World Series.
- World Series is always the final round.
- First playoff round is best of 3.
- Second playoff round is best of 5.
- Later playoff rounds are best of 7.
- Home field starts with seeding and qualification status.
- District champions receive home advantage over non-district champions.
- Runners-up receive home advantage over wild cards.
- Wild cards can only gain home advantage over other wild cards.
- When seeding is otherwise equal, rankings determine home advantage.
- Three-game series use higher seed home, lower seed home, higher seed home.
- Playoff results are stored in season history.
- Playoff player statistics are tracked separately from regular-season stats.

## Championship Flow

- Final championship series winner sets `season.ChampionTeamId`.
- Championship screen displays the winning team name, colors, scoreboard abbreviation, logo, and team photos when available.
- Championship title is `Season (#) World Champions!` with the current season number.
- Back-to-back champions use `BACK TO BACK WORLD CHAMPIONS!`.
- World Series championship sound is played at the conclusion of the final World Series game.
- Championship results persist in dynasty history.
- Championship badges can be generated and shown on team pages.

## Series Badges

- Series champion badge templates are supported.
- Badge templates use team school colors.
- Badge text includes the series name.
- Badge text includes school name and mascot.
- Badge text includes the current season number.
- Teams receive badges after winning configured series.
- Team pages display earned badges.
- Badge strip pages can be exported permanently.

## Rankings

- Every team receives a ranking.
- The Official Poll lists the top 25 teams unless the league has fewer than 25 teams.
- Preseason polls are supported.
- Week-to-week polls are supported.
- Final polls are supported.
- First-season preseason poll uses varsity roster team rating, senior class, and coach level.
- Later preseason polls can use previous-season information.
- Last regular-season poll is used for playoff seeding.
- Final poll is derived from the full season.
- World Series champion is ranked number 1 in the final poll.
- Poll ties show the higher tied ranking for all tied teams and note the tie.
- Polls can be exported individually.
- All polls can be exported together.
- Top 10 teams receive a hidden 5 percent in-game boost against teams outside the top 10.
- Top 25 teams receive a hidden 1 percent in-game boost against teams outside the top 10 and top 25.

## All-Star System

- All-Star selections are generated each season.
- Selected players receive an All-Star tag.
- All-Star Game is played after the championship and before the offseason.
- All-Star Game supports Player vs Player.
- All-Star Game supports Player vs CPU.
- All-Star Game supports CPU vs CPU/watch.
- All-Star Game has its own preview screen.
- All-Star Game uses the patriotic preview asset.
- All-Star teams use the shared lineup engine.
- All-Star injuries are reset before the game.
- Each All-Star team has three players per position.
- Each All-Star team has three DH selections.
- Each All-Star team has 12 pitchers.
- Pitcher all-stars who qualify at a fielding position can pitch in extra innings.
- Normal All-Star pitching plan uses five starters, two long relievers, one setup pitcher, and one closer.
- Each scheduled All-Star pitcher pitches one inning.
- If tied after regular innings, remaining pitchers pitch one inning each.
- If either side runs out of pitchers, the All-Star Game ends in a tie.
- All-Star Game stats are tracked.
- Career All-Star stats are tracked for players.
- Team-side All-Star history and leaders are tracked.

## Awards

- Position award system.
- Top five candidate tracking for award races.
- Award history is stored season by season.
- Position-player awards are supported.
- Pitching awards are supported.
- Gold Glove-style defensive awards are supported.
- Silver Bat-style hitting awards are supported.
- Cy Young Award is used for the wins-based pitching award.
- Nolan Ryan Award is used for the strikeout leader.
- Babe Ruth Award is used for the home run leader.
- Chuck Knoblauch Award is used for the doubles leader.
- John Wetteland Award is used for the saves leader.
- Rusty Greer Award is used for utility player.
- Ivan Rodriguez Award is used for catcher.
- Johnny Oates Award is given to the World Series winning coach as Coach of the Year.
- Johnny Oates Award qualifies the coach for Hall of Fame consideration.

## Hall of Fame

- Player Hall of Fame tracking.
- Coach Hall of Fame tracking.
- Team Hall of Fame page.
- Hall of Fame candidates can be reviewed.
- Hall of Fame pages can be exported.
- Hitter scoring includes games, hits, home runs, RBI, stolen bases, and OPS above .650.
- Pitcher scoring includes pitching performance categories.
- District stat leaders receive 25 Hall of Fame points.
- Region stat leaders receive 50 Hall of Fame points.
- Conference stat leaders receive 75 Hall of Fame points.
- League stat leaders receive 100 Hall of Fame points.
- Pitching stat leaders receive the same district, region, conference, and league point tiers.
- Superior playoff performance adds Hall of Fame points.
- Playoff Hall of Fame scoring can use hits, home runs, RBI, stolen bases, wins, saves, strikeouts, innings, OPS, ERA, and WHIP.
- Early-season career extrapolation helps players from the first three dynasty seasons.
- First-season seniors have their results added three times for career comparison.
- First-season juniors use a two-year average multiplied by two.
- First-season sophomores have their average added one additional time.
- First-season freshmen are left unchanged.
- Coach Hall of Fame points emphasize championships, playoff wins, district wins, region wins, and conference wins.

## Team Hall of Fame Page

- Displays World Series badges.
- Displays team logo.
- Displays season-by-season results rows.
- Season rows can include records and finish, such as `Season 1 50-12 Semi-Finalists`.
- Displays year-by-year player awards.
- Displays All-Star selections.
- Displays players finishing top five in statistics.
- Team Hall of Fame page can be exported.

## Statistics

- Team statistics pages.
- Player statistics pages.
- League statistics pages.
- Conference statistics pages.
- Region statistics pages.
- District statistics pages.
- Team-level pages include team and player stats.
- Non-team hierarchy pages include team information and leaders.
- Stats are sortable.
- Scope selector supports Season, Career, and All-Time where applicable.
- Player Stats includes All Seasons aggregation.
- Team Stats includes career/team aggregate modes.
- Playoff stats are tracked separately.
- All-Star stats are tracked separately.
- Career All-Star stats are tracked.
- Export support exists for stat pages and team pages.
- Exports are saved in Excel-compatible or Word-compatible document formats.

## Tracked Batting Stats

- Games.
- Plate appearances.
- Runs.
- At bats.
- Hits.
- Doubles.
- Triples.
- Home runs.
- RBI.
- Walks.
- Intentional walks.
- Strikeouts.
- Stolen bases.
- Caught stealing.
- Hit by pitch.
- Sacrifice hits.
- Sacrifice flies.
- Fly outs.
- Ground outs.
- Pop outs.
- Grounded into double plays.
- Extra-base hits.
- Reached on error.
- OPS-related derived values.

## Tracked Pitching Stats

- Innings pitched.
- Earned runs.
- Runs allowed.
- Strikeouts.
- Hits allowed.
- Doubles allowed.
- Triples allowed.
- Walks allowed.
- Intentional walks allowed.
- Home runs allowed.
- Hit batters.
- Wild pitches.
- Balks.
- Batters faced.
- Pitch count.
- Wins.
- Losses.
- Saves.
- Holds.
- Complete games.
- Shutouts.
- ERA-related derived values.
- WHIP-related derived values.

## Tracked Fielding and Availability Stats

- Putouts.
- Assists.
- Errors.
- Defensive innings.
- Total chances.
- Defensive double plays.
- Team double plays.
- Passed balls.
- Stolen bases allowed by catchers.
- Catcher caught stealing and caught-stealing percentage.
- Injury games missed.
- Games missed due to injury or roster status.

## Records Book

- League records.
- Conference records.
- Region records.
- District records.
- Team records.
- Records can be computed from stored game results and player lines.
- Records include team game records and player game records.
- Records include pitching, batting, fielding, and special event categories.

## Inbox and Scouting Mail

- Email-style inbox for the coach/user.
- Game events can generate inbox messages.
- Results can generate inbox messages.
- Statistics can generate inbox messages.
- Player of the game information can generate inbox messages.
- Scouting emails are sent before a series.
- Scouting emails summarize opponent strengths, lineup issues, and pitching matchup concerns.

## Fields and Stadiums

- Built-in selectable baseball fields.
- 20 historic stadium presets based on the supplied oldest-active-stadium image.
- ACU Baseball Field preset.
- The Ballpark in Arlington preset.
- Field of Dreams preset.
- Custom supplied field image presets.
- Users can create custom fields.
- Users can edit field colors.
- Users can assign background images.
- Users can place overlays, team logos, and other images.
- Custom fields can be assigned as team home fields.
- Custom fields can be exported as reusable packages.

## Cutscenes

- League-level cutscenes.
- Team-level cutscenes.
- Cutscenes can use videos or images.
- Cutscenes can trigger on configured game events.
- League cutscene editor.
- Team cutscene editor.
- National Anthem cutscene support.
- Settings can choose League Cut Scene, Team Cut Scene, or current game settings as the default.
- Team-only gameplay triggers include Home Run, Grand Slam, Run Scored, Strikeout, Pitcher Change, and Final Out.
- League-level triggers include Game Start, Playoff Game Start, All-Star Game Start, National Anthem, Seventh Inning Stretch, and Championship Won.

## Game Presentation

- Enabled home-team scoreboard templates replace the generic gameplay HUD in playable games, watched CPU games, and gameplay-rendered replays.
- Custom scoreboards show the live score, inning half, ball-strike-out count, home logo, school name, abbreviation, mascot, configured color layout, background, and advertising strip.
- Disabling the home scoreboard template retains the compact generic gameplay HUD.
- Loading screen shows team logos, `vs`, and each team's win-loss record.
- Loading screen title changes by game type, including District Game, Region Game, Conference Game, and playoff round names.
- Game start supports home and visitor starting lineup presentation.
- Home lineup screen uses the green lineup template style.
- Visitor lineup screen uses a black field style.
- Player sprites are placed in their defensive positions.
- Batting order is listed with player names.
- Team logo appears above the field and players.
- Position announcement audio plays during lineup presentation.
- Highlighting follows the player being announced.
- National Anthem sequence displays flags in center field above the wall.
- Visitor team lines up between third and home.
- Home players come out as called and line up between first and home.

## Postgame Flow

- Gameplay creates a final game result.
- Results dialog shows the winning team's logo.
- Results dialog shows the updated win-loss record.
- Results dialog includes a box score recap.
- User can commit the result to the selected scheduled game/season.
- User can dismiss the result.
- Postgame music loops until commit or dismiss.
- Confirmation sound plays after the user closes the postgame dialog.
- User returns to the Game tab after postgame flow.

## Replay

- Structured replay model.
- Portable replay store under the current Windows user's local application data; no developer desktop path is used.
- Replay Library dialog with explicit Import Replay, Watch, Remove, Open Folder, and Save Template actions.
- Imported files are copied into the managed library, duplicate names are preserved with numbered filenames, malformed JSON is rejected with a visible error, and incomplete-but-readable files remain available for best-effort or snapshot playback.
- A packaged editable schema-v2 replay template and `ExactReplaySchema.md` guide can be saved anywhere the user chooses.
- Replay watcher form.
- Replay files can be loaded from the replay folder.
- Replay files support play-by-play data, teams, lineups, and game events.
- Replay files can embed the complete home scoreboard template for exact presentation playback.
- Older replay files fall back to the matching dynasty team's scoreboard or the legacy replay scoreboard asset, then to the generic HUD when no custom board is available.
- External programs can export replay files for the user to import; the game does not depend on an external program or machine-specific source folder.

## Audio and Music

- Launch theme.
- Launch start-click sound.
- Loading screen music.
- Menu page music.
- Menu click sound.
- Team playlist music.
- Default simulated-game fallback music.
- Game intro music.
- Opening music.
- Play Ball call.
- Random National Anthem selection.
- Top-half inning loop music.
- Bottom-half inning loop music.
- Change-side music.
- Seventh-inning or final-inning stretch music.
- Home-team runners-on-base music.
- Visitor-team runners-on-base music.
- Runner-on-third music.
- Pitcher-change music.
- Pitch thrown sound.
- Bat-hit sound.
- In-play chance music.
- Strike calls with randomized strike audio.
- Ball call.
- Walk or take-your-base call.
- Foul ball call.
- Foul fly thud sound.
- Fly ball call.
- Safe call.
- Out call.
- Third-out call.
- Crowd cheer.
- Run scored sound.
- Home run randomized sounds.
- Grand slam sound.
- Hit-by-pitch or error sound.
- Top of 3rd special music.
- Top of 4th special music.
- Top of 7th special music when applicable.
- Final half-inning special music.
- Game over sound.
- Postgame loop.
- That's the Ball Game sound.
- World Series Champions sound.
- Position announcement audio for lineup presentation.

## Imports and External Libraries

- An optional shared audio, image, and video library can be selected or created during dynasty setup and changed later in Settings; new libraries receive Audio, Images, Video, and Teams folders, fresh installations leave the path unconfigured until the user chooses one, and assigned assets are copied into portable league/team asset folders.
- Imported team assets are copied into team folders for publishable leagues.
- Roster spreadsheet importer supports `.xlsx` roster files where available.
- Schools CSV importer supports creating teams from the saved app copy at `Assets\Data\schools.csv`.
- Users can replace the saved schools CSV with a newer version to add teams for future team creation.
- ROM snapshot importer can read data from the referenced NES ROM to seed standalone data.

## Export Features

- Stat pages can be exported.
- Team pages can be exported.
- Polls can be exported individually or together.
- Hall of Fame can be exported.
- Team Hall of Fame page can be exported.
- Badge strip pages can be exported permanently.
- Custom fields can be exported.
- Replay files can be exported for watch mode.
