# Dan's RBI Baseball 2026 Exact Replay Schema

This document defines the replay JSON format that another program, such as DanVille, must export for Dan's RBI Baseball 2026 to reproduce a watched game exactly as it occurred.

The existing `.rbi-replay.json` format can show event snapshots. Exact replay requires more: every pitch, input choice, animation path, timing point, runner movement, fielding action, audio cue, cutscene trigger, and validation state must be exported.

## File Rules

- File extension: `.rbi-replay.json`
- Encoding: UTF-8
- Naming policy: snake_case JSON property names
- Coordinate system: normalized field coordinates, where `x` and `y` are `0.0` to `1.0`
- Time units: milliseconds from replay start unless otherwise noted
- IDs: stable strings from the source game; never reuse an ID for a different team/player/game object
- All timestamps in metadata use ISO-8601 strings
- Exact playback requires `replay_schema_version` `2` or higher
- The game imports user-selected files into `%LOCALAPPDATA%\DanVille50\Dan's RBI Baseball 2026\Replays`; it never reads a developer desktop folder.
- Use **Replays > Save Template...** to save an editable exact-replay example and this format guide, then use **Import Replay...** after editing it.
- Relative media paths are resolved from the imported replay's managed-library location. Place companion media in that folder (or its subfolders) through **Open Folder** and keep paths relative for portability.

## Runtime Support

Schema version 2 is validated by the replay loader and played through the gameplay renderer. A complete file is labeled **Exact**. During playback the runtime interpolates recorded ball, throw, fielder, and runner paths against the replay clock; applies timed scoreboard changes; plays recorded audio and cutscene cues; and commits each recorded `after` state at the event boundary.

An incomplete schema version 2 file is opened as **Best Effort**, not rejected. The runtime preserves every usable timing point, path, snapshot, audio cue, and cutscene; reconstructs missing `before`/`after` states where possible; uses snapshot or default rendering for missing movement; skips unavailable media; and displays an approximation report in the replay window.

Schema version 1 remains supported as a legacy snapshot replay. It advances event summaries without claiming deterministic ball flight or animation timing.

## Top-Level Object

```json
{
  "replay_schema_version": 2,
  "source": "DanVille2026.sim",
  "source_version": "2026.07",
  "exported_at": "2026-07-13T18:30:00-05:00",
  "deterministic": true,
  "game": {},
  "rules": {},
  "teams": {},
  "assets": {},
  "starting_state": {},
  "events": [],
  "final_state": {},
  "validation": {}
}
```

## `game`

```json
{
  "game_id": "G00001",
  "season_id": "season-1",
  "season_number": 1,
  "scheduled_game_id": "schedule-game-guid-or-source-id",
  "game_type": "district",
  "playoff_round_name": "",
  "innings": 9,
  "stadium_id": "ballpark-at-arlington",
  "stadium_name": "The Ballpark at Arlington",
  "date_played": "2026-06-14",
  "winner_team_id": "home-team-id",
  "final_score": { "away": 4, "home": 5 },
  "line_score": {
    "away_runs_by_inning": [0, 1, 0, 0, 2, 0, 1, 0, 0],
    "home_runs_by_inning": [0, 0, 0, 3, 0, 0, 1, 0, 1],
    "away_hits": 8,
    "home_hits": 9,
    "away_errors": 1,
    "home_errors": 0,
    "away_left_on_base": 6,
    "home_left_on_base": 7
  }
}
```

Allowed `game_type` values:

- `non_conference`
- `district`
- `region`
- `conference`
- `playoff`
- `world_series`
- `all_star`
- `exhibition`

## `rules`

```json
{
  "mercy_rule_enabled": true,
  "mercy_rule_runs": 10,
  "mercy_rule_minimum_inning": 5,
  "extra_innings_enabled": true,
  "extra_inning_runner_on_second": true,
  "courtesy_runner_for_pitchers_catchers": true,
  "designated_hitter_enabled": true,
  "automatic_intentional_walk": true,
  "pitcher_fatigue_enabled": true,
  "injuries_enabled": true,
  "balks_enabled": true,
  "wild_pitches_passed_balls_enabled": true
}
```

## `teams`

```json
{
  "away": {
    "team_id": "away-team-id",
    "team_name": "Danville",
    "mascot": "Tigers",
    "scoreboard_abbreviation": "DAN",
    "primary_color": "#163B8F",
    "secondary_color": "#FFFFFF",
    "logo_path": "Assets/Teams/Danville/logo.png",
    "uniform_key": "visitor",
    "record_before_game": { "wins": 12, "losses": 4, "ties": 0 },
    "lineup": [],
    "bench": [],
    "pitching_staff": []
  },
  "home": {
    "team_id": "home-team-id",
    "team_name": "Arlington",
    "mascot": "Eagles",
    "scoreboard_abbreviation": "ARL",
    "primary_color": "#B00020",
    "secondary_color": "#FFFFFF",
    "logo_path": "Assets/Teams/Arlington/logo.png",
    "uniform_key": "home",
    "scoreboard_template": {
      "enabled": true,
      "template_name": "East View",
      "background_asset_path": "Assets/Scoreboards/east-view-scoreboard-template.jpg",
      "school_name_text": "Arlington",
      "preferred_abbreviation": "ARL",
      "mascot_text": "Eagles",
      "board_color_layout": 3,
      "board_argb": -5242880,
      "board_second_argb": -16777216,
      "board_third_argb": -1,
      "board_fourth_argb": -5242880,
      "accent_argb": -1,
      "text_argb": -1,
      "ad_strip_argb": -15132391,
      "ads": ["BOOSTER CLUB", "LOCAL BANK", "HOME OF THE EAGLES"]
    },
    "record_before_game": { "wins": 14, "losses": 3, "ties": 0 },
    "lineup": [],
    "bench": [],
    "pitching_staff": []
  }
}
```

`home.scoreboard_template` is the portable snapshot used by live gameplay and replay playback. Include it to reproduce the exact board design that was active when the game was played. `board_color_layout` values are `0` solid, `1` vertical halves, `2` horizontal halves, and `3` quarters. ARGB colors are signed 32-bit integers, matching the team editor save model.

For backward compatibility, replays without this object use the matching home team from the current dynasty when available. If no dynasty team can be matched, `assets.scoreboard_template` is used as a legacy JSON template or background image. Otherwise the generic HUD remains active.

### Team Player Shape

Every player that appears in the game must be listed in `lineup`, `bench`, or `pitching_staff`.

```json
{
  "player_id": "player-source-id",
  "name": "Logan Hessler",
  "number": 30,
  "team_id": "away-team-id",
  "position": "P",
  "eligible_positions": ["P", "1B"],
  "classification": "Senior",
  "player_type": "pitcher",
  "handedness": "R",
  "bats": "R",
  "throws": "R",
  "photo": "Assets/Teams/Danville/players/logan.png",
  "sprite_sheet": "Assets/Teams/Danville/sprites/logan.png",
  "ratings": {
    "contact": 64,
    "power": 52,
    "speed": 48,
    "base_running": 55,
    "pitching": 78,
    "stamina": 82,
    "fielding": 61,
    "throwing": 70,
    "catcher_blocking": 0,
    "catcher_arm": 0
  },
  "pitch_arsenal": [
    { "pitch_type": "Fastball", "effectiveness": 82 },
    { "pitch_type": "Curveball", "effectiveness": 71 },
    { "pitch_type": "Forkball", "effectiveness": 68 }
  ]
}
```

### Lineup Slot Shape

```json
{
  "order": 1,
  "position": "CF",
  "player": {}
}
```

Use `position: "DH"` for a designated hitter.

Required defensive positions:

- `P`
- `C`
- `1B`
- `2B`
- `3B`
- `SS`
- `LF`
- `CF`
- `RF`

## `assets`

This section maps replay trigger keys to image, video, and sound files. For portable imports, paths should be relative to the replay file in the managed Replay Library whenever possible.

```json
{
  "stadium_background": "Assets/Fields/ballpark_at_arlington.jpg",
  "scoreboard_template": "Assets/Scoreboards/arlington_template.json",
  "national_anthem_image": "Assets/Flags/Flags.png",
  "audio": {
    "intro": ["Assets/Game Sound Effects/01 Main Theme.mp3"],
    "opening": ["Assets/Game Sound Effects/02 Opening.mp3"],
    "play_ball": "Assets/Game Sound Effects/03 Play Ball.mp3",
    "top_half_loop": "Assets/Game Sound Effects/top_half_music.mp3",
    "bottom_half_loop": "Assets/Game Sound Effects/bottom_half_music.mp3",
    "strike": ["Assets/Game Sound Effects/11 Strike.mp3", "Assets/Game Sound Effects/18 Stee-rike.mp3"],
    "ball": "Assets/Game Sound Effects/20 Ball.mp3",
    "bat_contact": "Assets/Game Sound Effects/14 Baseball Bat Hits Ball.mp3",
    "in_play": "Assets/Game Sound Effects/04 Chance BGM.mp3",
    "safe": "Assets/Game Sound Effects/24 Safe.mp3",
    "out": "Assets/Game Sound Effects/23 Out.mp3",
    "third_out": "Assets/Game Sound Effects/22 You're Out.mp3",
    "run_scored": "Assets/Game Sound Effects/07 Scored a Run.mp3",
    "game_over": "Assets/Game Sound Effects/09 Game Over.mp3"
  },
  "cutscenes": {
    "national_anthem": "league",
    "homerun": "team",
    "grand_slam": "team",
    "strikeout": "team",
    "pitcher_change": "team",
    "final_out": "team"
  }
}
```

## `starting_state`

The starting state lets RBI Baseball 2026 initialize the renderer before the first replay event.

```json
{
  "time_ms": 0,
  "inning": 1,
  "half": "top",
  "outs": 0,
  "balls": 0,
  "strikes": 0,
  "score": { "away": 0, "home": 0 },
  "bases": { "first": null, "second": null, "third": null },
  "current_batter_id": "away-batter-1",
  "current_pitcher_id": "home-pitcher-1",
  "away_batter_index": 0,
  "home_batter_index": 0,
  "away_pitcher_index": 0,
  "home_pitcher_index": 0,
  "fielders": []
}
```

## `events`

Each event is one exact replay command. A complete replay should contain pregame events, every pitch, every dead-ball command, every substitution, every runner movement, every fielding throw, inning changes, and the final-out event.

```json
{
  "sequence": 1,
  "event_id": "G00001-E000001",
  "event_type": "pitch",
  "time_ms": 12000,
  "duration_ms": 1800,
  "inning": 1,
  "half": "top",
  "before": {},
  "command": {},
  "animation": {},
  "audio": [],
  "cutscenes": [],
  "result": {},
  "after": {},
  "validation": {}
}
```

### Required Event Fields

- `sequence`: unique increasing integer starting at `1`
- `event_id`: stable unique event ID
- `event_type`: one of the supported event types below
- `time_ms`: when the event begins
- `duration_ms`: how long the event animation lasts
- `before`: full game state before the event
- `command`: user/CPU action being replayed
- `animation`: exact visual movement instructions
- `result`: baseball result of the event
- `after`: full game state after the event
- `validation`: checksum/stat totals to prove the event applied correctly

### Supported Event Types

- `pregame_lineup`
- `national_anthem`
- `play_ball`
- `pitch`
- `ball_in_play`
- `foul_ball`
- `walk`
- `intentional_walk`
- `hit_by_pitch`
- `strikeout`
- `single`
- `double`
- `triple`
- `home_run`
- `grand_slam`
- `sacrifice_bunt`
- `sacrifice_fly`
- `steal_attempt`
- `pickoff_attempt`
- `balk`
- `wild_pitch`
- `passed_ball`
- `error`
- `fielder_choice`
- `double_play`
- `substitution`
- `pitcher_change`
- `mound_visit`
- `strategy_change`
- `runner_advance`
- `half_inning_end`
- `game_end`
- `championship_end`

## Game State Shape

Use this for `before`, `after`, and validation snapshots.

```json
{
  "inning": 1,
  "half": "top",
  "outs": 1,
  "balls": 2,
  "strikes": 1,
  "score": { "away": 0, "home": 0 },
  "bases": {
    "first": { "player_id": "runner-1", "responsible_pitcher_id": "pitcher-1", "earned": true },
    "second": null,
    "third": null
  },
  "current_batter_id": "batter-1",
  "current_pitcher_id": "pitcher-1",
  "away_batter_index": 0,
  "home_batter_index": 0,
  "away_pitcher_index": 0,
  "home_pitcher_index": 0,
  "dh_state": {
    "away_dh_active": true,
    "home_dh_active": true,
    "away_dh_player_id": "away-dh",
    "home_dh_player_id": "home-dh"
  },
  "live_rules": {
    "away_starter_pitch_count": 0,
    "home_starter_pitch_count": 14,
    "away_mound_visits_this_inning": 0,
    "home_mound_visits_this_inning": 0,
    "pitchers_removed_by_run_rule": [],
    "relief_pitcher_fatigue": {},
    "pitcher_run_rules": {}
  }
}
```

## Pitch Command Shape

Every pitch must include pitch type, target location, actual location, timing, batter choice, and defensive/offensive strategy.

```json
{
  "pitch": {
    "pitch_number": 27,
    "pitcher_id": "pitcher-1",
    "batter_id": "batter-1",
    "pitch_type": "Fastball",
    "pitch_effectiveness": 82,
    "target_location": { "zone_x": 0.44, "zone_y": 0.31 },
    "actual_location": { "zone_x": 0.47, "zone_y": 0.35 },
    "velocity_mph": 91,
    "spin_rpm": 2240,
    "break": { "x": -0.03, "y": 0.07 },
    "release_time_ms": 12040,
    "plate_time_ms": 12490,
    "called_zone": "strike",
    "pitcher_fatigue_adjustment_percent": -10
  },
  "batter_input": {
    "swing": true,
    "swing_type": "normal",
    "timing": "slightly_late",
    "timing_offset_ms": 38,
    "aim": { "zone_x": 0.50, "zone_y": 0.36 },
    "contact_quality": 72
  },
  "strategy": {
    "offense": "hit_and_run",
    "defense": "double_play",
    "steal_defense": "hold_runner",
    "coach_call_quality": "right"
  }
}
```

Allowed pitch types:

- `Fastball`
- `Changeup`
- `Curveball`
- `Slider`
- `Sinker`
- `Splitter`
- `Forkball`
- `Knuckleball`

Allowed swing types:

- `take`
- `normal`
- `contact`
- `power`
- `bunt`
- `hit_and_run`

Allowed timing values:

- `very_early`
- `early`
- `slightly_early`
- `perfect`
- `slightly_late`
- `late`
- `very_late`

## Animation Shape

The animation section is what makes replay exact instead of approximate.

```json
{
  "camera": {
    "view": "gameplay",
    "start_zoom": 1.0,
    "end_zoom": 1.0,
    "shake": false
  },
  "ball_path": [
    { "time_ms": 12040, "x": 0.50, "y": 0.62, "z": 0.10, "visible": true },
    { "time_ms": 12230, "x": 0.49, "y": 0.72, "z": 0.16, "visible": true },
    { "time_ms": 12490, "x": 0.47, "y": 0.84, "z": 0.12, "visible": true }
  ],
  "fielder_paths": [
    {
      "player_id": "ss-1",
      "position": "SS",
      "path": [
        { "time_ms": 12600, "x": 0.41, "y": 0.62 },
        { "time_ms": 13100, "x": 0.45, "y": 0.58 }
      ]
    }
  ],
  "runner_paths": [
    {
      "player_id": "runner-1",
      "from_base": 1,
      "to_base": 2,
      "path": [
        { "time_ms": 12580, "x": 0.68, "y": 0.70 },
        { "time_ms": 13400, "x": 0.59, "y": 0.62 }
      ],
      "safe": true
    }
  ],
  "throws": [
    {
      "from_player_id": "ss-1",
      "to_player_id": "1b-1",
      "start_time_ms": 13150,
      "catch_time_ms": 13650,
      "path": [
        { "time_ms": 13150, "x": 0.45, "y": 0.58, "z": 0.10 },
        { "time_ms": 13650, "x": 0.68, "y": 0.70, "z": 0.08 }
      ]
    }
  ],
  "highlight_player_ids": ["ss-1"],
  "scoreboard_updates_at_ms": [13680]
}
```

### Animation Requirements

For exact replay, these arrays must not be omitted when movement occurs:

- `ball_path`
- `fielder_paths`
- `runner_paths`
- `throws`

If no movement occurs, provide an empty array.

## Result Shape

```json
{
  "outcome": "single",
  "description": "Smith singles to left. Jones scores.",
  "detailed_description": "Fastball middle-away, normal swing, line drive to left field.",
  "narration_text": "Smith finds the gap and Jones comes home.",
  "runs_scored_on_play": 1,
  "rbi_player_ids": ["batter-1"],
  "earned_run": true,
  "batter": { "player_id": "batter-1" },
  "pitcher": { "player_id": "pitcher-1" },
  "fielder": { "player_id": "lf-1" },
  "assist_player_ids": [],
  "putout_player_id": "",
  "error_player_id": "",
  "winning_pitcher_id": "",
  "losing_pitcher_id": "",
  "save_pitcher_id": ""
}
```

Allowed `outcome` values:

- `ball`
- `called_strike`
- `swinging_strike`
- `foul`
- `foul_fly`
- `single`
- `double`
- `triple`
- `home_run`
- `grand_slam`
- `walk`
- `intentional_walk`
- `hit_by_pitch`
- `strikeout`
- `groundout`
- `flyout`
- `popout`
- `lineout`
- `sacrifice_bunt`
- `sacrifice_fly`
- `fielder_choice`
- `double_play`
- `error`
- `stolen_base`
- `caught_stealing`
- `picked_off`
- `balk`
- `wild_pitch`
- `passed_ball`
- `substitution`
- `pitcher_change`
- `mound_visit`
- `half_inning_end`
- `game_end`

## Runner Advancement Shape

Use structured runner advancement records instead of only text.

```json
{
  "runner_advancements": [
    {
      "player_id": "runner-1",
      "from_base": 1,
      "to_base": 3,
      "scored": false,
      "out": false,
      "safe": true,
      "reason": "single",
      "responsible_pitcher_id": "pitcher-1",
      "earned": true
    },
    {
      "player_id": "runner-2",
      "from_base": 3,
      "to_base": 4,
      "scored": true,
      "out": false,
      "safe": true,
      "reason": "single",
      "responsible_pitcher_id": "pitcher-1",
      "earned": true
    }
  ]
}
```

Base values:

- `0`: batter/home before play or removed from bases
- `1`: first base
- `2`: second base
- `3`: third base
- `4`: home plate/scored

## Audio Cue Shape

```json
{
  "audio": [
    {
      "cue": "bat_contact",
      "asset_key": "bat_contact",
      "file": "Assets/Game Sound Effects/14 Baseball Bat Hits Ball.mp3",
      "start_time_ms": 12520,
      "loop": false,
      "duck_background": true
    },
    {
      "cue": "safe",
      "asset_key": "safe",
      "start_time_ms": 13680,
      "loop": false
    }
  ]
}
```

Required cue names for exact replay:

- `pitch_throw`
- `strike`
- `ball`
- `foul_ball`
- `bat_contact`
- `in_play`
- `safe`
- `out`
- `third_out`
- `run_scored`
- `home_run`
- `grand_slam`
- `take_your_base`
- `hit_batter_or_error`
- `pitcher_change`
- `change_side`
- `game_over`
- `world_series_champion`

## Cutscene Shape

```json
{
  "cutscenes": [
    {
      "trigger": "home_run",
      "level": "team",
      "team_id": "home-team-id",
      "uniform_key": "home",
      "asset_path": "Assets/Teams/Arlington/cutscenes/HOME/home_run.mp4",
      "start_time_ms": 14000,
      "duration_ms": 5000,
      "blocking": true
    }
  ]
}
```

Allowed `level` values:

- `league`
- `team`

Allowed `uniform_key` values:

- `home`
- `home_alternate`
- `visitor`
- `visitor_alternate`
- `any`

## Substitution Shape

```json
{
  "event_type": "substitution",
  "command": {
    "team_id": "home-team-id",
    "substitution_type": "pitcher_change",
    "out_player_id": "old-pitcher",
    "in_player_id": "new-pitcher",
    "batting_order_slot": 9,
    "defensive_position": "P",
    "dh_lost": false,
    "reason": "pitcher_fatigue"
  }
}
```

Allowed `substitution_type` values:

- `pitcher_change`
- `pinch_hitter`
- `pinch_runner`
- `defensive_replacement`
- `courtesy_runner`
- `injury_replacement`
- `emergency_pitcher`

## Statistics Delta Shape

Each event must provide stat deltas so the replay can verify player and team totals exactly.

```json
{
  "stat_deltas": {
    "batting": [
      { "player_id": "batter-1", "ab": 1, "h": 1, "rbi": 1 }
    ],
    "pitching": [
      { "player_id": "pitcher-1", "batters_faced": 1, "hits_allowed": 1, "er": 1, "pitch_count": 1 }
    ],
    "fielding": [
      { "player_id": "lf-1", "putouts": 0, "assists": 0, "errors": 0 }
    ],
    "team": {
      "away": { "runs": 1, "hits": 1, "errors": 0, "left_on_base_delta": 0 },
      "home": { "runs": 0, "hits": 0, "errors": 0, "left_on_base_delta": 0 }
    }
  }
}
```

## Validation Shape

Validation allows RBI Baseball 2026 to stop or warn if the replay does not reproduce the exact state.

```json
{
  "validation": {
    "state_hash_before": "sha256-before",
    "state_hash_after": "sha256-after",
    "score_after": { "away": 1, "home": 0 },
    "outs_after": 1,
    "bases_after": {
      "first_player_id": "batter-1",
      "second_player_id": "",
      "third_player_id": "runner-1"
    },
    "team_hits_after": { "away": 1, "home": 0 },
    "team_errors_after": { "away": 0, "home": 0 },
    "pitch_count_after": {
      "pitcher-1": 27
    }
  }
}
```

Hash input should include:

- inning
- half
- count
- outs
- score
- base runner IDs
- batting order indices
- pitcher indices
- current pitcher IDs
- live rule state
- cumulative player stat totals

## Minimal Exact Pitch Event Example

```json
{
  "sequence": 44,
  "event_id": "G00001-E000044",
  "event_type": "single",
  "time_ms": 92750,
  "duration_ms": 4100,
  "inning": 3,
  "half": "top",
  "before": {
    "inning": 3,
    "half": "top",
    "outs": 1,
    "balls": 1,
    "strikes": 1,
    "score": { "away": 1, "home": 0 },
    "bases": {
      "first": { "player_id": "away-7", "responsible_pitcher_id": "home-p1", "earned": true },
      "second": null,
      "third": null
    },
    "current_batter_id": "away-2",
    "current_pitcher_id": "home-p1"
  },
  "command": {
    "pitch": {
      "pitch_number": 44,
      "pitcher_id": "home-p1",
      "batter_id": "away-2",
      "pitch_type": "Slider",
      "target_location": { "zone_x": 0.38, "zone_y": 0.42 },
      "actual_location": { "zone_x": 0.41, "zone_y": 0.44 },
      "velocity_mph": 82,
      "release_time_ms": 92780,
      "plate_time_ms": 93240
    },
    "batter_input": {
      "swing": true,
      "swing_type": "contact",
      "timing": "perfect",
      "timing_offset_ms": 4,
      "contact_quality": 81
    },
    "strategy": {
      "offense": "hit_and_run",
      "defense": "double_play",
      "steal_defense": "hold_runner"
    }
  },
  "animation": {
    "ball_path": [
      { "time_ms": 92780, "x": 0.50, "y": 0.62, "z": 0.10, "visible": true },
      { "time_ms": 93240, "x": 0.41, "y": 0.84, "z": 0.11, "visible": true },
      { "time_ms": 93520, "x": 0.32, "y": 0.48, "z": 0.05, "visible": true }
    ],
    "fielder_paths": [],
    "runner_paths": [
      {
        "player_id": "away-7",
        "from_base": 1,
        "to_base": 3,
        "path": [
          { "time_ms": 93250, "x": 0.68, "y": 0.70 },
          { "time_ms": 96250, "x": 0.32, "y": 0.70 }
        ],
        "safe": true
      }
    ],
    "throws": []
  },
  "audio": [
    { "cue": "pitch_throw", "start_time_ms": 92780 },
    { "cue": "bat_contact", "start_time_ms": 93245 },
    { "cue": "safe", "start_time_ms": 96250 }
  ],
  "cutscenes": [],
  "result": {
    "outcome": "single",
    "description": "Johnson singles to left. Miller moves to third.",
    "runs_scored_on_play": 0,
    "batter": { "player_id": "away-2" },
    "pitcher": { "player_id": "home-p1" },
    "fielder": { "player_id": "home-lf" }
  },
  "runner_advancements": [
    {
      "player_id": "away-7",
      "from_base": 1,
      "to_base": 3,
      "scored": false,
      "out": false,
      "safe": true,
      "reason": "single",
      "responsible_pitcher_id": "home-p1",
      "earned": true
    },
    {
      "player_id": "away-2",
      "from_base": 0,
      "to_base": 1,
      "scored": false,
      "out": false,
      "safe": true,
      "reason": "single",
      "responsible_pitcher_id": "home-p1",
      "earned": true
    }
  ],
  "after": {
    "inning": 3,
    "half": "top",
    "outs": 1,
    "balls": 0,
    "strikes": 0,
    "score": { "away": 1, "home": 0 },
    "bases": {
      "first": { "player_id": "away-2", "responsible_pitcher_id": "home-p1", "earned": true },
      "second": null,
      "third": { "player_id": "away-7", "responsible_pitcher_id": "home-p1", "earned": true }
    },
    "current_batter_id": "away-3",
    "current_pitcher_id": "home-p1"
  },
  "validation": {
    "score_after": { "away": 1, "home": 0 },
    "outs_after": 1,
    "bases_after": {
      "first_player_id": "away-2",
      "second_player_id": "",
      "third_player_id": "away-7"
    }
  }
}
```

## Export Requirements for DanVille

DanVille must export:

1. Full team metadata, colors, logos, uniforms, and all players used in the game.
2. Starting lineups, bench, pitcher staff, DH state, and defensive assignments.
3. A complete `starting_state`.
4. One event per replayable command, not just one event per plate appearance.
5. Full `before` and `after` state on every event.
6. Exact pitch data for every pitch.
7. Exact ball, runner, fielder, and throw paths for every animated play.
8. Exact audio cue timing.
9. Exact cutscene trigger timing.
10. Stat deltas and validation snapshots after every event.
11. A complete `final_state`.

If any of these are missing, RBI Baseball 2026 can still show a replay, but it cannot guarantee exact visual reproduction.
