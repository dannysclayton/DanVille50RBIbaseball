# Gameplay Presentation Specification

## Goal

Dan's RBI Baseball 2026 uses its existing shared rules engine for outcomes, statistics,
fatigue, injuries, strategy, and season play. The playable presentation must make those
outcomes visible as an actual baseball play instead of displaying a static field diagram.
The primary gameplay screen is a perspective 3D stadium presentation. The former overhead
GDI field remains only as a compatibility fallback and optional tactical reference.

The target combines three proven approaches:

- **R.B.I. Baseball (NES):** readable field geometry, large players, immediate controls,
  and a camera that follows the live ball without losing the defensive alignment.
- **Triple Play Baseball:** a close pitcher/batter view, rapid transition to the active
  fielder, direct base-button throws, urgency near the ball, diving, and wall play.
- **MVP Baseball 2005:** pitch selection and location, timing-based hitting, visible throw
  strength, contextual fielding movement, runner control, broadcast overlays, and smooth
  transitions between pitch, fielding, close-play, and dead-ball presentation.

Research references:

- R.B.I. Baseball manual and gameplay: https://www.gamingalexandria.com/highquality/NES/R.B.I.%20Baseball/R.B.I.%20Baseball%20-%20Manual.pdf
- R.B.I. Baseball gameplay footage: https://www.youtube.com/watch?v=BU6Rp1FSZDo
- Triple Play Baseball gameplay footage: https://www.youtube.com/watch?v=eYNfgFhwdQI
- Triple Play Baseball gameplay analysis: https://www.gamespot.com/articles/triple-play-baseball-hands-on/1100-2682336/
- MVP Baseball 2005 gameplay footage: https://www.youtube.com/watch?v=I_ThOGGEYig
- MVP Baseball 2005 gameplay analysis: https://www.gamespot.com/reviews/mvp-baseball-2005-review/1900-6119297/

## World Coordinates

The C# game engine uses one normalized field coordinate system. Home plate is near `(0.50, 0.86)`,
the mound near `(0.50, 0.62)`, second base near `(0.50, 0.58)`, and center field extends
toward the top of the world. Rules and actors remain in world coordinates; the camera is
only a projection. The Three.js bridge converts the compressed infield portion and expanded
outfield portion separately so the mound, bases, middle infield, and wall retain baseball-like
perspective depth.

Every 3D player model is anchored by the feet at its world position. The model may scale
with camera zoom and depth, but its feet may not drift away from the base, mound, fielding
spot, or runner path.

## Camera Phases

1. **At Bat:** Behind-the-batter view centered on the pitcher and mound. The batter occupies
   the correct left or right batter's box. The catcher is behind the camera and does not
   obstruct the pitch. Pitch path, count, runners, and immediate infield context remain readable.
2. **Ball Tracking:** On contact, smoothly cut to a wider view centered between the ball
   and active fielder. Keep the nearest bases and advancing runners visible.
3. **Throw to Base:** Follow the ball from fielder/cutoff player to the target base. Tighten
   enough to show the runner-fielder race without hiding the next required play.
4. **Dead Ball:** Briefly hold the result, then return to the at-bat camera for the next pitch.
5. **Replay/Watch:** Exact replay coordinates override automatic camera targeting; snapshot
   replays use the same phase rules as normal gameplay.

Camera changes must be eased and must never place active players outside the playable view.

## Player Presentation

- The primary renderer uses articulated 3D players with jersey, pants, and cap colors from
  the selected matchup uniform set.
- Player feet are the anchor and shadows sit under the feet.
- Pitcher windup, batter stance/swing, runner movement, fielder pursuit, catch, throw, slide,
  tag, safe, and out actions use animated 3D poses.
- Generated/imported 64x64 sprite sheets remain available to the tactical fallback renderer.
- Perspective scaling is bounded so outfielders remain identifiable and nearby players do
  not become oversized.

## Ball and Play Phases

- A pitch travels from the pitcher's release point to the selected plate location with pitch-
  specific break and a visible but restrained trail.
- Contact creates a ground ball, line drive, fly ball, or home-run arc. The ball has height,
  shadow, travel time, and a landing/intercept point.
- The selected fielder pursues the intercept point. CPU assistance can establish the first
  step, but the user retains movement and throw control when controlling the defense.
- A caught fly ball resolves at the catch. A fielded ground ball continues into a visible
  throw. Hits and errors continue into cutoff/base throws when a play is possible.
- Throws travel from the fielder to the selected base. Runner and ball arrival times determine
  safe/out presentation; the rules engine remains authoritative for the recorded result.
- Runners move along the base paths, not directly between arbitrary screen points.

## Controls

- Pitch: choose pitch, aim location, begin delivery, set power, and release for accuracy.
- Hit: contact, normal, power, or bunt; timing and aim influence contact quality and direction.
- Field: move the active fielder, switch fielders, dive/jump, and throw using diamond-mapped
  base buttons. Holding a throw button builds strength and excessive strength increases error
  risk.
- Run: select a runner/base, advance, retreat, lead, steal, and choose a slide on close plays.
- CPU and watch modes issue the same commands through the same gameplay state.

## Acceptance Criteria

- A default generated-roster game always displays uniformed 3D players.
- Batter, catcher, pitcher, all fielders, runners, and ball align with field anchors.
- Pitch, contact, pursuit, catch/field, throw, base play, and result are visible phases.
- Camera transitions retain the active ball, fielder, target base, and relevant runner.
- Playable, CPU-vs-CPU, simulation watch, and replay use the same renderer and coordinates.
- Browser regression checks require nonblank WebGL canvas pixels and screenshots at 16:9
  and compact 4:3-like sizes. C# tests validate the live state payload and offline assets.
