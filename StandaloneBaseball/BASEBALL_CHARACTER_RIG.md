# Baseball Character Rig

## Runtime Asset

`Assets/Gameplay3D/models/player_base.glb` is the user-created Meshy baseball
player used by the 3D gameplay renderer. The runtime clones its skinned mesh
with `SkeletonUtils.clone`, applies the current team's uniform colors, and gives
every on-field player an independent `THREE.AnimationMixer` and skeleton.

`baseball-character-rig.js` retains the procedural player as an offline fallback
when a packaged GLB cannot load. It also supplies deterministic baseball motions
that Meshy does not currently provide. No player model or animation is taken
from RBI Baseball, Triple Play, MVP Baseball, or another commercial game.

## Bone Contract

The current Meshy/Blender asset retains these deform-bone names:

- `Hips`, `Spine`, `Spine01`, `Spine02`, `neck`, `Head`
- `LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand`
- `RightShoulder`, `RightArm`, `RightForeArm`, `RightHand`
- `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase`
- `RightUpLeg`, `RightLeg`, `RightFoot`, `RightToeBase`

The runtime normalizes the Meshy centimeter-scale armature to a 5.25-unit
player. Bats align to both hand positions. Gloves attach to the non-throwing
hand (`LeftHand` for right-handed throwers and `RightHand` for left-handed
throwers), covering the source model's open rigging hand.

## Animation Contract

The gameplay-facing animation library contains:

- `FieldReady`
- `BatterIdle_R`, `BatterIdle_L`
- `Swing_R`, `Swing_L`
- `Pitch_R`, `Pitch_L`
- `Run`
- `Throw_R`, `Throw_L`
- `Catch`
- `CatcherCrouch`
- `RunnerLead`
- `Slide`
- `Celebrate`

The Meshy files provide `Pitch_R`, `Run`, and `Walk`. The runtime mirrors the
imported joint rotations into `Pitch_L` for left-handed pitchers and maps its
procedural baseball clips onto the Meshy bone names for the remaining actions.
Imported clips retain joint rotations while runtime placement owns world/root
translation; this prevents Meshy's centimeter-space root motion from moving a
player away from the mound or base path.
Gameplay can seek one-shot clips by normalized event progress, keeping playable
games, simulations, saved games, and exact replays synchronized.

## Blender/glTF Pipeline

1. Rig one full-body character with the Meshy bone names above.
2. Keep no more than four deform-bone influences per vertex.
3. Export the skinned pitching, running, and walking actions as GLB files.
4. Run `tools/BaseballCharacterPipeline/build_character_asset.py` through
   Blender 5.2 LTS.
5. The pipeline removes generated uniform lettering, creates named recolorable
   materials, saves the editable `.blend`, and exports one skinned base plus
   lightweight animation-only run/walk GLBs.
6. Automated tests validate the packaged skins, materials, and named clips.

glTF carries skeletons, skin weights, physically based materials, and named
animations in one portable format. Runtime uniform materials are `DRBI_Jersey`,
`DRBI_Pants`, `DRBI_Cap`, `DRBI_Accent`, and `DRBI_SkinDetail`.

## State Binding

- Pitch progress seeks `Pitch_R` or `Pitch_L`.
- Swing progress seeks `Swing_R` or `Swing_L`.
- A live batted ball selects `Run` for the batter and active fielder.
- Throw and close-play camera phases select `Throw_*` and `Catch`.
- Catchers use `CatcherCrouch`; occupied bases use `RunnerLead`.
- Team-selected jersey, pants, and cap colors are applied when a rig is built.

## Performance Rules

Each player owns independent mixer and skeleton state while all clones share the
base geometry and texture. Per-team materials and equipment are disposed when a
uniform or lineup rebuilds the scene. Frame delta is capped and only visible
gameplay actors cast shadows.
