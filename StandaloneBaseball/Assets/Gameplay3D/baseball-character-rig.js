import * as THREE from './three.module.min.js';
import { GLTFLoader } from './addons/loaders/GLTFLoader.js';
import { clone as cloneSkeleton } from './addons/utils/SkeletonUtils.js';

export const BASEBALL_ANIMATION_NAMES = Object.freeze([
  'FieldReady', 'BatterIdle_R', 'BatterIdle_L', 'Swing_R', 'Swing_L',
  'StrikeoutReaction_R', 'StrikeoutReaction_L', 'CalledStrikeReaction', 'StrikeoutWalkAway',
  'Pitch_R', 'Pitch_L', 'Run', 'RunnerBrakeAtFirst', 'RunnerStopAtSecond', 'Throw_R', 'Throw_L', 'Catch',
  'FielderPickup', 'RelayReceive',
  'CatcherCrouch', 'CatcherReceive', 'CatcherPopThrow_R', 'CatcherPopThrow_L',
  'PitcherStrikeoutReset', 'UmpireSet', 'UmpireStrikeout', 'UmpireSafe', 'UmpireOut',
  'SweepTag', 'RunnerLead', 'Slide', 'Celebrate'
]);

const BONE_NAMES = Object.freeze([
  'Hips', 'Spine', 'Chest', 'Neck', 'Head',
  'UpperArm_L', 'LowerArm_L', 'Hand_L',
  'UpperArm_R', 'LowerArm_R', 'Hand_R',
  'UpperLeg_L', 'LowerLeg_L', 'Foot_L',
  'UpperLeg_R', 'LowerLeg_R', 'Foot_R'
]);

const REST_HIPS_Y = 2.32;
const q = (x = 0, y = 0, z = 0) => new THREE.Quaternion().setFromEuler(new THREE.Euler(x, y, z, 'XYZ'));

function quaternionTrack(name, times, rotations) {
  const values = [];
  for (const rotation of rotations) values.push(...q(...rotation).toArray());
  return new THREE.QuaternionKeyframeTrack(`${name}.quaternion`, times, values);
}

function positionTrack(name, times, positions) {
  return new THREE.VectorKeyframeTrack(`${name}.position`, times, positions.flat());
}

function repeated(value, count) {
  return Array.from({ length: count }, () => value);
}

function clip(name, duration, definitions) {
  const tracks = [];
  for (const definition of definitions) {
    tracks.push(definition.kind === 'position'
      ? positionTrack(definition.bone, definition.times, definition.values)
      : quaternionTrack(definition.bone, definition.times, definition.values));
  }
  return new THREE.AnimationClip(name, duration, tracks);
}

function mirrored(rotations, axes = [false, true, true]) {
  return rotations.map(([x, y, z]) => [axes[0] ? -x : x, axes[1] ? -y : y, axes[2] ? -z : z]);
}

function createAnimationClips() {
  const loop = [0, .5, 1];
  const action = [0, .16, .34, .52, .72, 1];
  const run = [0, .25, .5, .75, 1];
  const batterLeft = [[-.88, .05, 1.03], [-.96, .05, 1.08], [-.88, .05, 1.03]];
  const batterRight = [[-.62, -.08, -1.2], [-.68, -.08, -1.16], [-.62, -.08, -1.2]];
  const foreLeft = [[-.55, 0, 1.12], [-.5, 0, 1.08], [-.55, 0, 1.12]];
  const foreRight = [[-.8, 0, -1.02], [-.76, 0, -.98], [-.8, 0, -1.02]];

  const clips = [
    clip('FieldReady', 1.2, [
      { kind: 'position', bone: 'Hips', times: loop, values: [[0, REST_HIPS_Y, 0], [0, REST_HIPS_Y - .07, .04], [0, REST_HIPS_Y, 0]] },
      { bone: 'Spine', times: loop, values: [[.08, 0, 0], [.11, 0, 0], [.08, 0, 0]] },
      { bone: 'UpperLeg_L', times: loop, values: repeated([-.18, 0, .04], 3) },
      { bone: 'UpperLeg_R', times: loop, values: repeated([-.18, 0, -.04], 3) },
      { bone: 'LowerLeg_L', times: loop, values: repeated([.28, 0, 0], 3) },
      { bone: 'LowerLeg_R', times: loop, values: repeated([.28, 0, 0], 3) },
      { bone: 'UpperArm_L', times: loop, values: repeated([-.18, 0, 1.18], 3) },
      { bone: 'UpperArm_R', times: loop, values: repeated([-.18, 0, -1.18], 3) },
      { bone: 'LowerArm_L', times: loop, values: repeated([-.2, 0, -.16], 3) },
      { bone: 'LowerArm_R', times: loop, values: repeated([-.2, 0, .16], 3) }
    ]),
    clip('BatterIdle_R', 1.1, [
      { kind: 'position', bone: 'Hips', times: loop, values: [[0, 2.13, 0], [0, 2.09, .02], [0, 2.13, 0]] },
      { bone: 'Spine', times: loop, values: [[.12, -.12, 0], [.15, -.12, 0], [.12, -.12, 0]] },
      { bone: 'Chest', times: loop, values: repeated([.03, -.18, 0], 3) },
      { bone: 'UpperArm_L', times: loop, values: batterLeft },
      { bone: 'LowerArm_L', times: loop, values: foreLeft },
      { bone: 'UpperArm_R', times: loop, values: batterRight },
      { bone: 'LowerArm_R', times: loop, values: foreRight },
      { bone: 'UpperLeg_L', times: loop, values: repeated([-.28, 0, .08], 3) },
      { bone: 'UpperLeg_R', times: loop, values: repeated([-.28, 0, -.08], 3) },
      { bone: 'LowerLeg_L', times: loop, values: repeated([.42, 0, 0], 3) },
      { bone: 'LowerLeg_R', times: loop, values: repeated([.42, 0, 0], 3) }
    ]),
    clip('BatterIdle_L', 1.1, [
      { kind: 'position', bone: 'Hips', times: loop, values: [[0, 2.13, 0], [0, 2.09, .02], [0, 2.13, 0]] },
      { bone: 'Spine', times: loop, values: [[.12, .12, 0], [.15, .12, 0], [.12, .12, 0]] },
      { bone: 'Chest', times: loop, values: repeated([.03, .18, 0], 3) },
      { bone: 'UpperArm_L', times: loop, values: mirrored(batterRight) },
      { bone: 'LowerArm_L', times: loop, values: mirrored(foreRight) },
      { bone: 'UpperArm_R', times: loop, values: mirrored(batterLeft) },
      { bone: 'LowerArm_R', times: loop, values: mirrored(foreLeft) },
      { bone: 'UpperLeg_L', times: loop, values: repeated([-.28, 0, .08], 3) },
      { bone: 'UpperLeg_R', times: loop, values: repeated([-.28, 0, -.08], 3) },
      { bone: 'LowerLeg_L', times: loop, values: repeated([.42, 0, 0], 3) },
      { bone: 'LowerLeg_R', times: loop, values: repeated([.42, 0, 0], 3) }
    ]),
    clip('Swing_R', 1, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.13,0],[0,2.08,.04],[0,2.02,.08],[0,2.08,.02],[0,2.18,-.02],[0,2.13,0]] },
      { bone: 'Hips', times: action, values: [[0,-.22,0],[0,-.34,0],[0,.05,0],[0,.62,0],[0,.92,0],[0,.7,0]] },
      { bone: 'Spine', times: action, values: [[.12,-.15,0],[.16,-.3,0],[.08,.18,0],[.03,.72,0],[.18,.9,0],[.1,.62,0]] },
      { bone: 'Chest', times: action, values: [[0,-.18,0],[0,-.32,0],[0,.2,0],[0,.82,0],[.08,1.02,0],[.04,.75,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.88,.05,1.03],[-1.0,.08,1.15],[-1.25,-.2,.45],[-1.1,-.85,-.1],[-.72,-1.2,-.55],[-.55,-.8,-.42]] },
      { bone: 'LowerArm_L', times: action, values: [[-.55,0,1.12],[-.5,0,1.2],[-.22,0,.4],[0,0,.08],[.18,0,.32],[.1,0,.2]] },
      { bone: 'UpperArm_R', times: action, values: [[-.62,-.08,-1.2],[-.7,-.1,-1.28],[-1.05,.25,-.5],[-1.12,.9,.1],[-.8,1.24,.58],[-.62,.88,.44]] },
      { bone: 'LowerArm_R', times: action, values: [[-.8,0,-1.02],[-.76,0,-1.1],[-.3,0,-.4],[0,0,-.08],[.15,0,-.3],[.08,0,-.18]] }
    ]),
    clip('Swing_L', 1, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.13,0],[0,2.08,.04],[0,2.02,.08],[0,2.08,.02],[0,2.18,-.02],[0,2.13,0]] },
      { bone: 'Hips', times: action, values: mirrored([[0,-.22,0],[0,-.34,0],[0,.05,0],[0,.62,0],[0,.92,0],[0,.7,0]]) },
      { bone: 'Spine', times: action, values: mirrored([[.12,-.15,0],[.16,-.3,0],[.08,.18,0],[.03,.72,0],[.18,.9,0],[.1,.62,0]]) },
      { bone: 'Chest', times: action, values: mirrored([[0,-.18,0],[0,-.32,0],[0,.2,0],[0,.82,0],[.08,1.02,0],[.04,.75,0]]) },
      { bone: 'UpperArm_L', times: action, values: mirrored([[-.62,-.08,-1.2],[-.7,-.1,-1.28],[-1.05,.25,-.5],[-1.12,.9,.1],[-.8,1.24,.58],[-.62,.88,.44]]) },
      { bone: 'LowerArm_L', times: action, values: mirrored([[-.8,0,1.02],[-.76,0,1.1],[-.3,0,.4],[0,0,-.08],[.15,0,-.3],[.08,0,-.18]]) },
      { bone: 'UpperArm_R', times: action, values: mirrored([[-.88,.05,1.03],[-1.0,.08,1.15],[-1.25,-.2,.45],[-1.1,-.85,-.1],[-.72,-1.2,-.55],[-.55,-.8,-.42]]) },
      { bone: 'LowerArm_R', times: action, values: mirrored([[-.55,0,-1.12],[-.5,0,-1.2],[-.22,0,-.4],[0,0,.08],[.18,0,.32],[.1,0,.2]]) }
    ]),
    clip('StrikeoutReaction_R', .95, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.13,0],[0,2.1,.02],[0,2.05,.04],[0,2.08,.02],[0,2.12,0],[0,2.13,0]] },
      { bone: 'Hips', times: action, values: [[0,.7,0],[0,.88,0],[0,.72,0],[0,.4,0],[0,.12,0],[0,-.1,0]] },
      { bone: 'Spine', times: action, values: [[.1,.62,0],[.16,.82,0],[.28,.65,0],[.34,.35,0],[.42,.1,0],[.34,-.08,0]] },
      { bone: 'Neck', times: action, values: [[0,0,0],[.08,0,0],[.2,0,0],[.32,0,0],[.38,0,0],[.3,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.55,-.8,-.42],[-.68,-1.05,-.52],[-.5,-.72,-.35],[-.35,-.32,.15],[-.2,0,.55],[-.15,0,.75]] },
      { bone: 'UpperArm_R', times: action, values: [[-.62,.88,.44],[-.76,1.1,.56],[-.52,.72,.38],[-.32,.28,-.15],[-.18,0,-.55],[-.15,0,-.75]] }
    ]),
    clip('StrikeoutReaction_L', .95, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.13,0],[0,2.1,.02],[0,2.05,.04],[0,2.08,.02],[0,2.12,0],[0,2.13,0]] },
      { bone: 'Hips', times: action, values: mirrored([[0,.7,0],[0,.88,0],[0,.72,0],[0,.4,0],[0,.12,0],[0,-.1,0]]) },
      { bone: 'Spine', times: action, values: mirrored([[.1,.62,0],[.16,.82,0],[.28,.65,0],[.34,.35,0],[.42,.1,0],[.34,-.08,0]]) },
      { bone: 'Neck', times: action, values: [[0,0,0],[.08,0,0],[.2,0,0],[.32,0,0],[.38,0,0],[.3,0,0]] },
      { bone: 'UpperArm_L', times: action, values: mirrored([[-.62,.88,.44],[-.76,1.1,.56],[-.52,.72,.38],[-.32,.28,-.15],[-.18,0,-.55],[-.15,0,-.75]]) },
      { bone: 'UpperArm_R', times: action, values: mirrored([[-.55,-.8,-.42],[-.68,-1.05,-.52],[-.5,-.72,-.35],[-.35,-.32,.15],[-.2,0,.55],[-.15,0,.75]]) }
    ]),
    clip('CalledStrikeReaction', .95, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.13,0],[0,2.08,.02],[0,2.03,.05],[0,2.08,.04],[0,2.13,.02],[0,2.13,0]] },
      { bone: 'Spine', times: action, values: [[.12,0,0],[.18,0,0],[.24,0,0],[.32,0,0],[.38,0,0],[.3,0,0]] },
      { bone: 'Neck', times: action, values: [[0,0,0],[.08,0,0],[.2,0,0],[.3,0,0],[.38,0,0],[.3,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.88,.05,1.03],[-.62,0,.88],[-.35,0,.68],[-.2,0,.52],[-.15,0,.72],[-.12,0,.82]] },
      { bone: 'UpperArm_R', times: action, values: [[-.62,-.08,-1.2],[-.48,0,-.92],[-.28,0,-.7],[-.18,0,-.52],[-.12,0,-.72],[-.1,0,-.82]] }
    ]),
    clip('StrikeoutWalkAway', .85, [
      { kind: 'position', bone: 'Hips', times: run, values: [[0,2.22,0],[0,2.31,.03],[0,2.22,0],[0,2.31,.03],[0,2.22,0]] },
      { bone: 'Spine', times: run, values: repeated([.2,0,0], 5) },
      { bone: 'Neck', times: run, values: repeated([.28,0,0], 5) },
      { bone: 'UpperLeg_L', times: run, values: [[-.55,0,0],[.1,0,0],[.55,0,0],[.1,0,0],[-.55,0,0]] },
      { bone: 'UpperLeg_R', times: run, values: [[.55,0,0],[-.1,0,0],[-.55,0,0],[-.1,0,0],[.55,0,0]] },
      { bone: 'LowerLeg_L', times: run, values: [[.45,0,0],[.2,0,0],[.72,0,0],[.2,0,0],[.45,0,0]] },
      { bone: 'LowerLeg_R', times: run, values: [[.72,0,0],[.2,0,0],[.45,0,0],[.2,0,0],[.72,0,0]] }
    ]),
    clip('Pitch_R', 1, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.38,.05],[0,2.45,.12],[0,2.2,.18],[0,2.15,.3],[0,REST_HIPS_Y,0]] },
      { bone: 'Hips', times: action, values: [[0,0,0],[0,-.35,0],[0,-.62,0],[0,.18,0],[0,.55,0],[0,.28,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[0,0,0],[-1.35,0,0],[-1.55,0,0],[-.35,0,0],[.62,0,0],[.2,0,0]] },
      { bone: 'LowerLeg_L', times: action, values: [[0,0,0],[1.12,0,0],[1.38,0,0],[.42,0,0],[.12,0,0],[0,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[0,0,0],[.08,0,0],[.16,0,0],[-.62,0,0],[-.38,0,0],[0,0,0]] },
      { bone: 'UpperArm_R', times: action, values: [[0,0,-.12],[-.75,.18,-.8],[-1.72,.3,-1.0],[-2.25,-.3,.2],[-.55,-.85,.62],[0,0,-.1]] },
      { bone: 'LowerArm_R', times: action, values: [[0,0,0],[-.65,0,.3],[-1.2,0,.65],[-.25,0,-.2],[.3,0,-.25],[0,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.3,0,.3],[-.55,0,.75],[-.7,0,.95],[-.3,0,.4],[.15,0,-.25],[-.25,0,.2]] }
    ]),
    clip('Pitch_L', 1, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.38,.05],[0,2.45,.12],[0,2.2,.18],[0,2.15,.3],[0,REST_HIPS_Y,0]] },
      { bone: 'Hips', times: action, values: mirrored([[0,0,0],[0,-.35,0],[0,-.62,0],[0,.18,0],[0,.55,0],[0,.28,0]]) },
      { bone: 'UpperLeg_R', times: action, values: [[0,0,0],[-1.35,0,0],[-1.55,0,0],[-.35,0,0],[.62,0,0],[.2,0,0]] },
      { bone: 'LowerLeg_R', times: action, values: [[0,0,0],[1.12,0,0],[1.38,0,0],[.42,0,0],[.12,0,0],[0,0,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[0,0,0],[.08,0,0],[.16,0,0],[-.62,0,0],[-.38,0,0],[0,0,0]] },
      { bone: 'UpperArm_L', times: action, values: mirrored([[0,0,-.12],[-.75,.18,-.8],[-1.72,.3,-1.0],[-2.25,-.3,.2],[-.55,-.85,.62],[0,0,-.1]]) },
      { bone: 'LowerArm_L', times: action, values: mirrored([[0,0,0],[-.65,0,.3],[-1.2,0,.65],[-.25,0,-.2],[.3,0,-.25],[0,0,0]]) },
      { bone: 'UpperArm_R', times: action, values: mirrored([[-.3,0,.3],[-.55,0,.75],[-.7,0,.95],[-.3,0,.4],[.15,0,-.25],[-.25,0,.2]]) }
    ]),
    clip('Run', .72, [
      { kind: 'position', bone: 'Hips', times: run, values: [[0,2.27,0],[0,2.38,.05],[0,2.27,0],[0,2.38,.05],[0,2.27,0]] },
      { bone: 'Spine', times: run, values: repeated([.22,0,0], 5) },
      { bone: 'UpperLeg_L', times: run, values: [[-.9,0,0],[0,0,0],[.9,0,0],[0,0,0],[-.9,0,0]] },
      { bone: 'UpperLeg_R', times: run, values: [[.9,0,0],[0,0,0],[-.9,0,0],[0,0,0],[.9,0,0]] },
      { bone: 'LowerLeg_L', times: run, values: [[.65,0,0],[.2,0,0],[.95,0,0],[.2,0,0],[.65,0,0]] },
      { bone: 'LowerLeg_R', times: run, values: [[.95,0,0],[.2,0,0],[.65,0,0],[.2,0,0],[.95,0,0]] },
      { bone: 'UpperArm_L', times: run, values: [[.65,0,.1],[0,0,.1],[-.65,0,.1],[0,0,.1],[.65,0,.1]] },
      { bone: 'UpperArm_R', times: run, values: [[-.65,0,-.1],[0,0,-.1],[.65,0,-.1],[0,0,-.1],[-.65,0,-.1]] },
      { bone: 'LowerArm_L', times: run, values: repeated([-1.0,0,0], 5) },
      { bone: 'LowerArm_R', times: run, values: repeated([-1.0,0,0], 5) }
    ]),
    clip('RunnerBrakeAtFirst', .78, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.27,0],[0,2.2,.06],[0,2.12,.12],[0,2.08,.08],[0,2.16,.02],[0,2.22,0]] },
      { bone: 'Spine', times: action, values: [[.22,0,0],[.34,.08,0],[.45,.16,0],[.38,.1,0],[.24,.04,0],[.16,0,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[-.9,0,0],[-.35,0,.08],[.38,0,.16],[.22,0,.12],[-.12,0,.06],[-.2,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[.9,0,0],[.28,0,-.08],[-.45,0,-.16],[-.25,0,-.12],[-.08,0,-.06],[-.2,0,0]] },
      { bone: 'LowerLeg_L', times: action, values: [[.65,0,0],[.38,0,0],[.72,0,0],[.52,0,0],[.34,0,0],[.28,0,0]] },
      { bone: 'LowerLeg_R', times: action, values: [[.95,0,0],[.45,0,0],[.68,0,0],[.48,0,0],[.32,0,0],[.28,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[.65,0,.1],[.25,0,.55],[-.25,0,.85],[-.18,0,.72],[-.12,0,.55],[-.15,0,.4]] },
      { bone: 'UpperArm_R', times: action, values: [[-.65,0,-.1],[-.25,0,-.55],[.25,0,-.85],[.18,0,-.72],[.12,0,-.55],[.15,0,-.4]] }
    ]),
    clip('RunnerStopAtSecond', .88, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.3,0],[0,2.24,.08],[0,2.12,.16],[0,2.08,.1],[0,2.18,.03],[0,2.24,0]] },
      { bone: 'Spine', times: action, values: [[.24,0,0],[.32,.06,0],[.42,.12,0],[.34,.08,0],[.18,.03,0],[.08,0,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[-.82,0,0],[-.28,0,.08],[.48,0,.18],[.18,0,.08],[-.12,0,.03],[-.18,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[.82,0,0],[.3,0,-.08],[-.5,0,-.18],[-.2,0,-.08],[-.1,0,-.03],[-.18,0,0]] },
      { bone: 'LowerLeg_L', times: action, values: [[.7,0,0],[.42,0,0],[.78,0,0],[.5,0,0],[.3,0,0],[.22,0,0]] },
      { bone: 'LowerLeg_R', times: action, values: [[.92,0,0],[.48,0,0],[.7,0,0],[.44,0,0],[.28,0,0],[.22,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[.58,0,.1],[.38,0,.14],[.18,0,.18],[.12,0,.12],[.16,0,.08],[.18,0,.06]] },
      { bone: 'UpperArm_R', times: action, values: [[-.58,0,-.1],[-.38,0,-.14],[-.18,0,-.18],[-.12,0,-.12],[-.16,0,-.08],[-.18,0,-.06]] },
      { bone: 'LowerArm_L', times: action, values: [[-1,0,0],[-.82,0,0],[-.62,0,0],[-.48,0,0],[-.36,0,0],[-.28,0,0]] },
      { bone: 'LowerArm_R', times: action, values: [[-1,0,0],[-.82,0,0],[-.62,0,0],[-.48,0,0],[-.36,0,0],[-.28,0,0]] }
    ]),
    clip('Throw_R', .8, [
      { bone: 'Hips', times: action, values: [[0,0,0],[0,-.25,0],[0,-.45,0],[0,.25,0],[0,.55,0],[0,.25,0]] },
      { bone: 'UpperArm_R', times: action, values: [[0,0,-.1],[-.8,.2,-.75],[-1.7,.25,-1.0],[-2.25,-.2,.1],[-.5,-.8,.65],[0,0,-.1]] },
      { bone: 'LowerArm_R', times: action, values: [[0,0,0],[-.6,0,.3],[-1.15,0,.55],[-.2,0,-.2],[.25,0,-.2],[0,0,0]] },
      { bone: 'UpperArm_L', times: action, values: repeated([-.45,0,.72], 6) }
    ]),
    clip('Throw_L', .8, [
      { bone: 'Hips', times: action, values: mirrored([[0,0,0],[0,-.25,0],[0,-.45,0],[0,.25,0],[0,.55,0],[0,.25,0]]) },
      { bone: 'UpperArm_L', times: action, values: mirrored([[0,0,-.1],[-.8,.2,-.75],[-1.7,.25,-1.0],[-2.25,-.2,.1],[-.5,-.8,.65],[0,0,-.1]]) },
      { bone: 'LowerArm_L', times: action, values: mirrored([[0,0,0],[-.6,0,.3],[-1.15,0,.55],[-.2,0,-.2],[.25,0,-.2],[0,0,0]]) },
      { bone: 'UpperArm_R', times: action, values: repeated([-.45,0,-.72], 6) }
    ]),
    clip('Catch', .62, [
      { bone: 'UpperArm_L', times: action, values: [[-.25,0,.2],[-.75,0,.48],[-1.25,0,.72],[-1.4,0,.82],[-.95,0,.55],[-.35,0,.25]] },
      { bone: 'LowerArm_L', times: action, values: [[0,0,0],[-.3,0,-.3],[-.5,0,-.65],[-.42,0,-.8],[-.25,0,-.45],[0,0,0]] },
      { bone: 'UpperArm_R', times: action, values: [[-.2,0,-.2],[-.62,0,-.36],[-1.0,0,-.5],[-1.12,0,-.58],[-.72,0,-.4],[-.25,0,-.2]] }
    ]),
    clip('FielderPickup', .78, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.05,.08],[0,1.62,.22],[0,1.38,.35],[0,1.68,.24],[0,2.08,.08]] },
      { bone: 'Spine', times: action, values: [[.08,0,0],[.28,0,0],[.62,.08,0],[.82,.12,0],[.55,.08,0],[.18,0,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[-.18,0,.04],[-.35,0,.08],[-.62,0,.14],[-.78,0,.2],[-.52,0,.14],[-.24,0,.06]] },
      { bone: 'UpperLeg_R', times: action, values: [[-.18,0,-.04],[-.32,0,-.08],[-.58,0,-.14],[-.72,0,-.2],[-.48,0,-.14],[-.22,0,-.06]] },
      { bone: 'UpperArm_L', times: action, values: [[-.18,0,1.18],[-.48,0,.86],[-.92,.08,.48],[-1.35,.18,.12],[-1.05,.1,.38],[-.42,0,.82]] },
      { bone: 'LowerArm_L', times: action, values: [[-.2,0,-.16],[-.42,0,-.28],[-.68,0,-.42],[-.92,0,-.55],[-.7,0,-.42],[-.32,0,-.24]] }
    ]),
    clip('RelayReceive', .68, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.2,.03],[0,2.08,.08],[0,2.02,.12],[0,2.1,.08],[0,2.18,.03]] },
      { bone: 'Spine', times: action, values: [[.08,0,0],[.15,0,0],[.28,0,0],[.34,0,0],[.24,0,0],[.12,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.18,0,1.18],[-.52,0,.92],[-.92,0,.58],[-1.2,0,.34],[-.9,0,.56],[-.38,0,.92]] },
      { bone: 'LowerArm_L', times: action, values: [[-.2,0,-.16],[-.38,0,-.32],[-.55,0,-.52],[-.68,0,-.68],[-.5,0,-.48],[-.28,0,-.26]] },
      { bone: 'UpperArm_R', times: action, values: [[-.18,0,-1.18],[-.42,0,-.82],[-.72,0,-.55],[-.9,0,-.38],[-.68,0,-.58],[-.32,0,-.92]] }
    ]),
    clip('CatcherCrouch', 1.2, [
      { kind: 'position', bone: 'Hips', times: loop, values: [[0,1.55,0],[0,1.5,.02],[0,1.55,0]] },
      { bone: 'Spine', times: loop, values: repeated([.32,0,0], 3) },
      { bone: 'UpperLeg_L', times: loop, values: repeated([-.85,0,.48], 3) },
      { bone: 'UpperLeg_R', times: loop, values: repeated([-.85,0,-.48], 3) },
      { bone: 'LowerLeg_L', times: loop, values: repeated([1.45,0,0], 3) },
      { bone: 'LowerLeg_R', times: loop, values: repeated([1.45,0,0], 3) },
      { bone: 'UpperArm_L', times: loop, values: repeated([-.55,0,.42], 3) },
      { bone: 'LowerArm_L', times: loop, values: repeated([-.55,0,-.55], 3) },
      { bone: 'UpperArm_R', times: loop, values: repeated([-.55,0,-.42], 3) },
      { bone: 'LowerArm_R', times: loop, values: repeated([-.55,0,.55], 3) }
    ]),
    clip('CatcherReceive', .58, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,1.55,0],[0,1.52,.01],[0,1.48,.02],[0,1.5,.01],[0,1.53,0],[0,1.55,0]] },
      { bone: 'Spine', times: action, values: [[.32,0,0],[.36,0,0],[.42,0,0],[.38,0,0],[.34,0,0],[.32,0,0]] },
      { bone: 'UpperLeg_L', times: action, values: repeated([-.85,0,.48], 6) },
      { bone: 'UpperLeg_R', times: action, values: repeated([-.85,0,-.48], 6) },
      { bone: 'LowerLeg_L', times: action, values: repeated([1.45,0,0], 6) },
      { bone: 'LowerLeg_R', times: action, values: repeated([1.45,0,0], 6) },
      { bone: 'UpperArm_L', times: action, values: [[-.55,0,.42],[-.72,0,.35],[-.95,0,.28],[-.82,0,.32],[-.64,0,.38],[-.55,0,.42]] },
      { bone: 'LowerArm_L', times: action, values: [[-.55,0,-.55],[-.72,0,-.62],[-.88,0,-.68],[-.76,0,-.62],[-.62,0,-.58],[-.55,0,-.55]] }
    ]),
    clip('PitcherStrikeoutReset', 1.4, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,.25],[0,2.25,.2],[0,2.3,.1],[0,2.34,0],[0,2.32,-.05],[0,REST_HIPS_Y,0]] },
      { bone: 'Hips', times: action, values: [[0,.35,0],[0,.28,0],[0,.18,0],[0,.08,0],[0,0,0],[0,0,0]] },
      { bone: 'Spine', times: action, values: [[.15,.28,0],[.12,.2,0],[.1,.12,0],[.08,.05,0],[.08,0,0],[.08,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.1,0,.32],[-.22,0,.65],[-.28,0,.88],[-.2,0,1.0],[-.18,0,1.12],[-.18,0,1.18]] },
      { bone: 'UpperArm_R', times: action, values: [[-.45,-.4,.4],[-.3,-.22,.22],[-.22,-.1,-.3],[-.18,0,-.72],[-.18,0,-1.0],[-.18,0,-1.18]] }
    ]),
    clip('UmpireSet', 1.2, [
      { kind: 'position', bone: 'Hips', times: loop, values: [[0,1.72,0],[0,1.68,.02],[0,1.72,0]] },
      { bone: 'Spine', times: loop, values: repeated([.28,0,0], 3) },
      { bone: 'UpperLeg_L', times: loop, values: repeated([-.62,0,.32], 3) },
      { bone: 'UpperLeg_R', times: loop, values: repeated([-.62,0,-.32], 3) },
      { bone: 'LowerLeg_L', times: loop, values: repeated([1.05,0,0], 3) },
      { bone: 'LowerLeg_R', times: loop, values: repeated([1.05,0,0], 3) }
    ]),
    clip('UmpireStrikeout', 1.1, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,1.72,0],[0,1.78,.02],[0,1.98,.04],[0,2.15,.08],[0,2.22,.08],[0,2.24,.06]] },
      { bone: 'Spine', times: action, values: [[.28,0,0],[.22,0,0],[.12,0,0],[.05,-.12,0],[.08,-.2,0],[.1,-.16,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[-.62,0,.32],[-.48,0,.24],[-.3,0,.12],[-.15,0,.06],[-.08,0,.03],[0,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[-.62,0,-.32],[-.48,0,-.24],[-.3,0,-.12],[-.15,0,-.06],[-.08,0,-.03],[0,0,0]] },
      { bone: 'LowerLeg_L', times: action, values: [[1.05,0,0],[.82,0,0],[.55,0,0],[.3,0,0],[.15,0,0],[0,0,0]] },
      { bone: 'LowerLeg_R', times: action, values: [[1.05,0,0],[.82,0,0],[.55,0,0],[.3,0,0],[.15,0,0],[0,0,0]] },
      { bone: 'UpperArm_R', times: action, values: [[-.25,0,-.35],[-.65,0,-.62],[-1.05,0,-.82],[-1.55,0,-1.0],[-1.95,0,-.72],[-1.75,0,-.5]] },
      { bone: 'LowerArm_R', times: action, values: [[-.2,0,.2],[-.55,0,.38],[-.9,0,.48],[-1.25,0,.35],[-1.42,0,.12],[-1.25,0,.08]] },
      { bone: 'UpperArm_L', times: action, values: [[-.25,0,.35],[-.35,0,.42],[-.42,0,.5],[-.35,0,.55],[-.28,0,.6],[-.22,0,.65]] }
    ]),
    clip('UmpireSafe', .9, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.28,.02],[0,2.24,.04],[0,2.22,.04],[0,2.26,.02],[0,REST_HIPS_Y,0]] },
      { bone: 'Spine', times: action, values: [[.08,0,0],[.12,0,0],[.18,0,0],[.16,0,0],[.1,0,0],[.08,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.2,0,.4],[-.58,0,.78],[-1.05,0,1.28],[-1.28,0,1.48],[-.82,0,1.02],[-.24,0,.46]] },
      { bone: 'UpperArm_R', times: action, values: [[-.2,0,-.4],[-.58,0,-.78],[-1.05,0,-1.28],[-1.28,0,-1.48],[-.82,0,-1.02],[-.24,0,-.46]] }
    ]),
    clip('UmpireOut', .92, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.25,.02],[0,2.3,.04],[0,2.34,.04],[0,2.32,.02],[0,2.3,0]] },
      { bone: 'Spine', times: action, values: [[.08,0,0],[.12,-.08,0],[.18,-.18,0],[.14,-.24,0],[.1,-.2,0],[.08,-.16,0]] },
      { bone: 'UpperArm_R', times: action, values: [[-.2,0,-.4],[-.55,0,-.62],[-1.05,0,-.82],[-1.48,0,-.92],[-1.55,0,-.82],[-1.42,0,-.72]] },
      { bone: 'LowerArm_R', times: action, values: [[-.15,0,.18],[-.48,0,.35],[-.92,0,.42],[-1.35,0,.28],[-1.48,0,.12],[-1.38,0,.08]] },
      { bone: 'UpperArm_L', times: action, values: [[-.2,0,.4],[-.28,0,.46],[-.34,0,.52],[-.32,0,.5],[-.28,0,.46],[-.24,0,.42]] }
    ]),
    clip('CatcherPopThrow_R', .92, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,1.55,0],[0,1.62,.03],[0,1.95,.1],[0,2.2,.2],[0,2.26,.3],[0,2.3,.34]] },
      { bone: 'Spine', times: action, values: [[.32,0,0],[.25,-.12,0],[.12,-.3,0],[.06,.2,0],[.18,.48,0],[.12,.32,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[-.85,0,.48],[-.62,0,.34],[-.3,0,.16],[-.16,0,.1],[-.08,0,.04],[0,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[-.85,0,-.48],[-.62,0,-.34],[-.3,0,-.16],[-.16,0,-.1],[-.08,0,-.04],[0,0,0]] },
      { bone: 'LowerLeg_L', times: action, values: [[1.45,0,0],[1.05,0,0],[.55,0,0],[.3,0,0],[.12,0,0],[0,0,0]] },
      { bone: 'LowerLeg_R', times: action, values: [[1.45,0,0],[1.05,0,0],[.55,0,0],[.3,0,0],[.12,0,0],[0,0,0]] },
      { bone: 'UpperArm_R', times: action, values: [[-.55,0,-.42],[-.75,.12,-.72],[-1.5,.2,-.95],[-2.15,-.24,.12],[-.55,-.8,.62],[0,0,-.1]] },
      { bone: 'LowerArm_R', times: action, values: [[-.55,0,.55],[-.75,0,.48],[-1.08,0,.62],[-.22,0,-.18],[.25,0,-.2],[0,0,0]] },
      { bone: 'UpperArm_L', times: action, values: [[-.55,0,.42],[-.62,0,.58],[-.45,0,.72],[-.28,0,.4],[.05,0,-.18],[-.2,0,.2]] }
    ]),
    clip('CatcherPopThrow_L', .92, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,1.55,0],[0,1.62,.03],[0,1.95,.1],[0,2.2,.2],[0,2.26,.3],[0,2.3,.34]] },
      { bone: 'Spine', times: action, values: mirrored([[.32,0,0],[.25,-.12,0],[.12,-.3,0],[.06,.2,0],[.18,.48,0],[.12,.32,0]]) },
      { bone: 'UpperLeg_L', times: action, values: [[-.85,0,.48],[-.62,0,.34],[-.3,0,.16],[-.16,0,.1],[-.08,0,.04],[0,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[-.85,0,-.48],[-.62,0,-.34],[-.3,0,-.16],[-.16,0,-.1],[-.08,0,-.04],[0,0,0]] },
      { bone: 'LowerLeg_L', times: action, values: [[1.45,0,0],[1.05,0,0],[.55,0,0],[.3,0,0],[.12,0,0],[0,0,0]] },
      { bone: 'LowerLeg_R', times: action, values: [[1.45,0,0],[1.05,0,0],[.55,0,0],[.3,0,0],[.12,0,0],[0,0,0]] },
      { bone: 'UpperArm_L', times: action, values: mirrored([[-.55,0,-.42],[-.75,.12,-.72],[-1.5,.2,-.95],[-2.15,-.24,.12],[-.55,-.8,.62],[0,0,-.1]]) },
      { bone: 'LowerArm_L', times: action, values: mirrored([[-.55,0,.55],[-.75,0,.48],[-1.08,0,.62],[-.22,0,-.18],[.25,0,-.2],[0,0,0]]) },
      { bone: 'UpperArm_R', times: action, values: mirrored([[-.55,0,.42],[-.62,0,.58],[-.45,0,.72],[-.28,0,.4],[.05,0,-.18],[-.2,0,.2]]) }
    ]),
    clip('SweepTag', .72, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.18,.03],[0,1.88,.1],[0,1.52,.18],[0,1.35,.24],[0,1.42,.26]] },
      { bone: 'Spine', times: action, values: [[.08,0,0],[.18,0,0],[.35,.18,0],[.52,.42,.08],[.62,.68,.12],[.48,.5,.08]] },
      { bone: 'UpperArm_L', times: action, values: [[-.18,0,1.18],[-.55,0,.72],[-.92,.1,.42],[-1.18,.35,.12],[-1.32,.62,-.18],[-1.1,.5,-.12]] },
      { bone: 'LowerArm_L', times: action, values: [[-.2,0,-.16],[-.42,0,-.35],[-.68,0,-.48],[-.84,0,-.62],[-.72,0,-.5],[-.55,0,-.38]] },
      { bone: 'UpperLeg_L', times: action, values: [[-.18,0,.04],[-.3,0,.08],[-.55,0,.16],[-.78,0,.24],[-.88,0,.28],[-.72,0,.22]] },
      { bone: 'LowerLeg_L', times: action, values: [[.28,0,0],[.45,0,0],[.72,0,0],[1.0,0,0],[1.12,0,0],[.95,0,0]] }
    ]),
    clip('RunnerLead', 1.0, [
      { kind: 'position', bone: 'Hips', times: loop, values: [[0,2.08,0],[.08,2.04,0],[0,2.08,0]] },
      { bone: 'Spine', times: loop, values: repeated([.25,0,0], 3) },
      { bone: 'UpperLeg_L', times: loop, values: [[-.35,0,.12],[-.2,0,.12],[-.35,0,.12]] },
      { bone: 'UpperLeg_R', times: loop, values: [[-.2,0,-.12],[-.35,0,-.12],[-.2,0,-.12]] },
      { bone: 'LowerLeg_L', times: loop, values: repeated([.5,0,0], 3) },
      { bone: 'LowerLeg_R', times: loop, values: repeated([.5,0,0], 3) },
      { bone: 'UpperArm_L', times: loop, values: repeated([-.25, 0, 1.08], 3) },
      { bone: 'UpperArm_R', times: loop, values: repeated([-.25, 0, -1.08], 3) }
    ]),
    clip('Slide', .85, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,2.25,0],[0,1.8,.2],[0,1.05,.55],[0,.65,1.0],[0,.52,1.5],[0,.5,1.8]] },
      { bone: 'Hips', times: action, values: [[0,0,0],[-.25,0,0],[-.65,0,0],[-1.05,0,0],[-1.28,0,0],[-1.35,0,0]] },
      { bone: 'UpperLeg_L', times: action, values: [[0,0,0],[-.35,0,0],[-.75,0,0],[-.25,0,0],[.12,0,0],[.18,0,0]] },
      { bone: 'UpperLeg_R', times: action, values: [[0,0,0],[-.2,0,0],[-.4,0,0],[-.8,0,0],[-1.0,0,0],[-1.1,0,0]] }
    ]),
    clip('Celebrate', 1.15, [
      { kind: 'position', bone: 'Hips', times: action, values: [[0,REST_HIPS_Y,0],[0,2.5,0],[0,2.78,0],[0,2.45,0],[0,2.68,0],[0,REST_HIPS_Y,0]] },
      { bone: 'UpperArm_L', times: action, values: [[0,0,.15],[-.6,0,.7],[-1.1,0,1.35],[-1.25,0,1.45],[-1.1,0,1.35],[-.25,0,.2]] },
      { bone: 'UpperArm_R', times: action, values: [[0,0,-.15],[-.6,0,-.7],[-1.1,0,-1.35],[-1.25,0,-1.45],[-1.1,0,-1.35],[-.25,0,-.2]] }
    ])
  ];
  return new Map(clips.map(animation => [animation.name, animation]));
}

const ANIMATION_CLIPS = createAnimationClips();

const MESHY_BONE_ALIASES = Object.freeze({
  Hips: 'Hips', Spine: 'Spine', Chest: 'Spine02', Neck: 'neck', Head: 'Head',
  UpperArm_L: 'LeftArm', LowerArm_L: 'LeftForeArm', Hand_L: 'LeftHand',
  UpperArm_R: 'RightArm', LowerArm_R: 'RightForeArm', Hand_R: 'RightHand',
  UpperLeg_L: 'LeftUpLeg', LowerLeg_L: 'LeftLeg', Foot_L: 'LeftFoot',
  UpperLeg_R: 'RightUpLeg', LowerLeg_R: 'RightLeg', Foot_R: 'RightFoot'
});

let meshyAssets = null;
let meshyAssetPromise = null;

function loadGltf(loader, path) {
  return new Promise((resolve, reject) => loader.load(path, resolve, undefined, reject));
}

export function initializeBaseballCharacterAssets() {
  if (meshyAssetPromise) return meshyAssetPromise;
  const loader = new GLTFLoader();
  meshyAssetPromise = Promise.all([
    loadGltf(loader, './models/player_base.glb'),
    loadGltf(loader, './models/player_run.glb'),
    loadGltf(loader, './models/player_walk.glb')
  ]).then(([base, run, walk]) => {
    const animations = new Map();
    for (const animation of [...base.animations, ...run.animations, ...walk.animations]) {
      animations.set(animation.name, animation);
    }
    meshyAssets = { scene: base.scene, animations };
    return true;
  }).catch(error => {
    console.error('Meshy baseball character assets failed to load; using fallback rig.', error);
    meshyAssets = null;
    return false;
  });
  return meshyAssetPromise;
}

function material(color, roughness = .74, metalness = .02) {
  return new THREE.MeshStandardMaterial({ color, roughness, metalness });
}

function shadow(mesh) {
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

function bone(name, x, y, z) {
  const value = new THREE.Bone();
  value.name = name;
  value.position.set(x, y, z);
  return value;
}

function addCylinder(parent, radiusTop, radiusBottom, length, appearance, y = -length / 2) {
  const mesh = shadow(new THREE.Mesh(new THREE.CylinderGeometry(radiusTop, radiusBottom, length, 12), appearance));
  mesh.position.y = y;
  parent.add(mesh);
  return mesh;
}

function buildSkeleton() {
  const bones = {
    Hips: bone('Hips', 0, REST_HIPS_Y, 0),
    Spine: bone('Spine', 0, .48, 0),
    Chest: bone('Chest', 0, .78, 0),
    Neck: bone('Neck', 0, .88, 0),
    Head: bone('Head', 0, .32, 0),
    UpperArm_L: bone('UpperArm_L', -.88, .62, 0),
    LowerArm_L: bone('LowerArm_L', 0, -.88, 0),
    Hand_L: bone('Hand_L', 0, -.78, 0),
    UpperArm_R: bone('UpperArm_R', .88, .62, 0),
    LowerArm_R: bone('LowerArm_R', 0, -.88, 0),
    Hand_R: bone('Hand_R', 0, -.78, 0),
    UpperLeg_L: bone('UpperLeg_L', -.38, -.06, 0),
    LowerLeg_L: bone('LowerLeg_L', 0, -1.08, 0),
    Foot_L: bone('Foot_L', 0, -1.06, -.04),
    UpperLeg_R: bone('UpperLeg_R', .38, -.06, 0),
    LowerLeg_R: bone('LowerLeg_R', 0, -1.08, 0),
    Foot_R: bone('Foot_R', 0, -1.06, -.04)
  };
  bones.Hips.add(bones.Spine, bones.UpperLeg_L, bones.UpperLeg_R);
  bones.Spine.add(bones.Chest);
  bones.Chest.add(bones.Neck, bones.UpperArm_L, bones.UpperArm_R);
  bones.Neck.add(bones.Head);
  bones.UpperArm_L.add(bones.LowerArm_L);
  bones.LowerArm_L.add(bones.Hand_L);
  bones.UpperArm_R.add(bones.LowerArm_R);
  bones.LowerArm_R.add(bones.Hand_R);
  bones.UpperLeg_L.add(bones.LowerLeg_L);
  bones.LowerLeg_L.add(bones.Foot_L);
  bones.UpperLeg_R.add(bones.LowerLeg_R);
  bones.LowerLeg_R.add(bones.Foot_R);
  return bones;
}

function createSkinnedTorso(bones, uniformMaterial) {
  const geometry = new THREE.CylinderGeometry(.78, .92, 1.72, 16, 5, false);
  geometry.translate(0, 3.58, 0);
  const position = geometry.attributes.position;
  const skinIndices = [];
  const skinWeights = [];
  const index = Object.fromEntries(BONE_NAMES.map((name, i) => [name, i]));
  for (let i = 0; i < position.count; i++) {
    const y = position.getY(i);
    if (y < 3.12) {
      skinIndices.push(index.Hips, index.Spine, 0, 0);
      skinWeights.push(.35, .65, 0, 0);
    } else if (y < 3.78) {
      skinIndices.push(index.Spine, index.Chest, 0, 0);
      const chestWeight = THREE.MathUtils.clamp((y - 3.12) / .66, 0, 1);
      skinWeights.push(1 - chestWeight, chestWeight, 0, 0);
    } else {
      skinIndices.push(index.Chest, 0, 0, 0);
      skinWeights.push(1, 0, 0, 0);
    }
  }
  geometry.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(skinIndices, 4));
  geometry.setAttribute('skinWeight', new THREE.Float32BufferAttribute(skinWeights, 4));
  const torso = shadow(new THREE.SkinnedMesh(geometry, uniformMaterial));
  const skeleton = new THREE.Skeleton(BONE_NAMES.map(name => bones[name]));
  torso.add(bones.Hips);
  torso.updateMatrixWorld(true);
  torso.bind(skeleton);
  return { torso, skeleton };
}

export class BaseballCharacterRig {
  constructor(options = {}) {
    this.object = new THREE.Group();
    this.object.name = `BaseballCharacter_${options.role || 'Player'}`;
    this.object.userData.characterRig = this;
    this.role = options.role || 'Fielder';
    this.throwHand = String(options.throwHand || 'R').toUpperCase();
    this.batHand = String(options.batHand || 'R').toUpperCase();
    this.bones = buildSkeleton();

    const uniform = material(options.primary || '#315fc4');
    const pants = material(options.secondary || '#f2f2f2');
    const cap = material(options.cap || options.primary || '#315fc4');
    const skin = material(options.skin || '#c98f65', .9);
    const shoe = material('#181b20', .82);
    const leather = material('#754522', .9);
    const wood = material('#c99b54', .58);
    this.materials = [uniform, pants, cap, skin, shoe, leather, wood];

    const skinned = createSkinnedTorso(this.bones, uniform);
    this.skeleton = skinned.skeleton;
    this.object.add(skinned.torso);
    this.skinnedMesh = skinned.torso;

    const belt = shadow(new THREE.Mesh(new THREE.CylinderGeometry(.88, .88, .16, 16), shoe));
    belt.position.y = .02;
    this.bones.Hips.add(belt);

    addCylinder(this.bones.UpperArm_L, .29, .34, .92, uniform);
    addCylinder(this.bones.UpperArm_R, .29, .34, .92, uniform);
    addCylinder(this.bones.LowerArm_L, .19, .23, .8, skin);
    addCylinder(this.bones.LowerArm_R, .19, .23, .8, skin);
    addCylinder(this.bones.UpperLeg_L, .34, .42, 1.12, pants);
    addCylinder(this.bones.UpperLeg_R, .34, .42, 1.12, pants);
    addCylinder(this.bones.LowerLeg_L, .27, .32, 1.08, pants);
    addCylinder(this.bones.LowerLeg_R, .27, .32, 1.08, pants);

    for (const hand of [this.bones.Hand_L, this.bones.Hand_R]) {
      const mesh = shadow(new THREE.Mesh(new THREE.SphereGeometry(.25, 12, 9), skin));
      mesh.scale.set(.86, 1.15, .8);
      hand.add(mesh);
    }
    for (const foot of [this.bones.Foot_L, this.bones.Foot_R]) {
      const mesh = shadow(new THREE.Mesh(new THREE.BoxGeometry(.62, .34, .98), shoe));
      mesh.position.set(0, -.1, -.28);
      foot.add(mesh);
    }

    const head = shadow(new THREE.Mesh(new THREE.SphereGeometry(.58, 18, 14), skin));
    head.position.y = .38;
    this.bones.Head.add(head);
    const capCrown = shadow(new THREE.Mesh(new THREE.SphereGeometry(.61, 18, 9, 0, Math.PI * 2, 0, Math.PI / 2), cap));
    capCrown.position.y = .58;
    this.bones.Head.add(capCrown);
    const brim = shadow(new THREE.Mesh(new THREE.BoxGeometry(.92, .11, .52), cap));
    brim.position.set(0, .5, -.43);
    this.bones.Head.add(brim);

    this.glove = shadow(new THREE.Mesh(new THREE.SphereGeometry(.38, 12, 8), leather));
    this.glove.scale.set(1.25, .78, .5);
    this.glove.position.set(0, -.18, -.12);
    const fallbackGloveBone = this.throwHand === 'L' ? this.bones.Hand_R : this.bones.Hand_L;
    fallbackGloveBone.add(this.glove);

    this.bat = shadow(new THREE.Mesh(new THREE.CylinderGeometry(.075, .13, 3.2, 12), wood));
    // The hand bone follows the forearm toward the grip, so the barrel extends
    // along negative local Y back over the hitter's shoulder.
    this.bat.position.set(0, -1.42, 0);
    this.bat.rotation.z = .08;
    this.bones.Hand_R.add(this.bat);

    this.setEquipment(this.role);
    this.mixer = new THREE.AnimationMixer(this.object);
    this.actions = new Map();
    for (const [name, animation] of ANIMATION_CLIPS) this.actions.set(name, this.mixer.clipAction(animation));
    this.currentName = '';
    this.currentSource = 'procedural-fallback';
    this.currentAction = null;
    this.play(this.role === 'Batter' ? `BatterIdle_${this.batHand}` : 'FieldReady');
  }

  setEquipment(role) {
    const batter = role === 'Batter' || role === 'Runner';
    this.bat.visible = role === 'Batter';
    this.glove.visible = !batter;
  }

  play(name, normalizedTime = null, loop = true) {
    if (!this.actions.has(name)) name = 'FieldReady';
    if (name !== this.currentName) {
      if (this.currentAction) this.currentAction.stop();
      this.skeleton.pose();
      this.currentAction = this.actions.get(name);
      this.currentAction.reset();
      this.currentAction.enabled = true;
      this.currentAction.clampWhenFinished = !loop;
      this.currentAction.setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce, loop ? Infinity : 1);
      this.currentAction.play();
      this.currentName = name;
    }
    if (normalizedTime != null) {
      const duration = this.currentAction.getClip().duration;
      this.currentAction.paused = true;
      this.currentAction.time = THREE.MathUtils.clamp(normalizedTime, 0, .9999) * duration;
      this.mixer.update(0);
    } else {
      this.currentAction.paused = false;
    }
  }

  update(deltaSeconds) {
    this.mixer.update(Math.min(.05, Math.max(0, deltaSeconds || 0)));
  }

  dispose() {
    this.mixer.stopAllAction();
    this.mixer.uncacheRoot(this.object);
    this.skinnedMesh.geometry.dispose();
    for (const appearance of this.materials) appearance.dispose();
    this.object.traverse(child => {
      if (child.isMesh && child !== this.skinnedMesh) child.geometry?.dispose();
    });
    this.skeleton.dispose();
  }
}

function cloneAndColorMaterial(source, options) {
  const appearance = source.clone();
  if (appearance.name.startsWith('DRBI_Jersey')) appearance.color.set(options.primary || '#315fc4');
  if (appearance.name.startsWith('DRBI_Pants')) appearance.color.set(options.secondary || '#f2f2f2');
  if (appearance.name.startsWith('DRBI_Cap')) appearance.color.set(options.cap || options.primary || '#315fc4');
  if (appearance.name.startsWith('DRBI_Accent')) appearance.color.set(options.primary || '#315fc4');
  appearance.needsUpdate = true;
  return appearance;
}

function adaptProceduralClip(source, bones) {
  const tracks = [];
  for (const sourceTrack of source.tracks) {
    const separator = sourceTrack.name.lastIndexOf('.');
    const sourceBoneName = sourceTrack.name.slice(0, separator);
    const property = sourceTrack.name.slice(separator + 1);
    const targetBoneName = MESHY_BONE_ALIASES[sourceBoneName];
    const targetBone = bones[targetBoneName];
    if (!targetBone) continue;

    if (property === 'quaternion') {
      const values = [];
      const rest = targetBone.quaternion.clone();
      for (let index = 0; index < sourceTrack.values.length; index += 4) {
        const delta = new THREE.Quaternion().fromArray(sourceTrack.values, index);
        values.push(...rest.clone().multiply(delta).normalize().toArray());
      }
      tracks.push(new THREE.QuaternionKeyframeTrack(
        `${targetBoneName}.quaternion`, sourceTrack.times, values
      ));
    } else if (property === 'position') {
      const values = [];
      const rest = targetBone.position;
      for (let index = 0; index < sourceTrack.values.length; index += 3) {
        values.push(rest.x, rest.y, rest.z);
      }
      tracks.push(new THREE.VectorKeyframeTrack(
        `${targetBoneName}.position`, sourceTrack.times, values
      ));
    }
  }
  return new THREE.AnimationClip(source.name, source.duration, tracks);
}

function sanitizeImportedClip(source) {
  const tracks = source.tracks
    .filter(track => track.name.endsWith('.quaternion'))
    .map(track => track.clone());
  return new THREE.AnimationClip(source.name, source.duration, tracks);
}

function mirroredBoneName(name) {
  if (name.startsWith('Left')) return `Right${name.slice(4)}`;
  if (name.startsWith('Right')) return `Left${name.slice(5)}`;
  return name;
}

function mirrorImportedClip(source, name) {
  const tracks = source.tracks
    .filter(track => track.name.endsWith('.quaternion'))
    .map(track => {
      const separator = track.name.lastIndexOf('.');
      const boneName = track.name.slice(0, separator);
      const values = track.values.slice();
      for (let index = 0; index < values.length; index += 4) {
        values[index + 1] *= -1;
        values[index + 2] *= -1;
      }
      return new THREE.QuaternionKeyframeTrack(
        `${mirroredBoneName(boneName)}.quaternion`,
        track.times.slice(),
        values
      );
    });
  return new THREE.AnimationClip(name, source.duration, tracks);
}

class MeshyBaseballCharacterRig {
  constructor(options = {}) {
    this.object = new THREE.Group();
    this.object.name = `MeshyBaseballCharacter_${options.role || 'Player'}`;
    this.object.userData.characterRig = this;
    this.role = options.role || 'Fielder';
    this.throwHand = String(options.throwHand || 'R').toUpperCase();
    this.batHand = String(options.batHand || 'R').toUpperCase();
    this.visual = cloneSkeleton(meshyAssets.scene);
    this.visual.name = 'DRBI_MeshyPlayerVisual';
    this.object.add(this.visual);

    this.materials = [];
    this.ownedGeometries = [];
    this.skeletons = [];
    this.bones = {};
    this.visual.traverse(child => {
      if (child.isBone) this.bones[child.name] = child;
      if (child.isSkinnedMesh && child.skeleton) this.skeletons.push(child.skeleton);
      if (!child.isMesh) return;
      child.castShadow = true;
      child.receiveShadow = true;
      if (Array.isArray(child.material)) {
        child.material = child.material.map(source => {
          const appearance = cloneAndColorMaterial(source, options);
          this.materials.push(appearance);
          return appearance;
        });
      } else if (child.material) {
        child.material = cloneAndColorMaterial(child.material, options);
        this.materials.push(child.material);
      }
    });

    for (const skeleton of this.skeletons) skeleton.pose();
    this.visual.updateMatrixWorld(true);
    const restBounds = new THREE.Box3().setFromObject(this.visual);
    const restSize = restBounds.getSize(new THREE.Vector3());
    const restCenter = restBounds.getCenter(new THREE.Vector3());
    const visualScale = 5.25 / Math.max(.001, restSize.y);
    this.visual.scale.setScalar(visualScale);
    this.visual.position.set(
      -restCenter.x * visualScale,
      -restBounds.min.y * visualScale,
      -restCenter.z * visualScale
    );

    const leather = material('#8a4b23', .88);
    const wood = material('#c99b54', .58);
    const battingGlove = material(options.secondary || '#f2f2f2', .86);
    this.materials.push(leather, wood, battingGlove);

    this.glove = shadow(new THREE.Mesh(new THREE.SphereGeometry(.135, 14, 10), leather));
    this.glove.scale.set(1.42, 1.0, .68);
    this.glove.position.set(0, -.025, .012);
    const gloveBone = this.throwHand === 'L' ? this.bones.RightHand : this.bones.LeftHand;
    gloveBone?.add(this.glove);
    this.ownedGeometries.push(this.glove.geometry);
    this.fieldingGloveCover = shadow(new THREE.Mesh(new THREE.SphereGeometry(.24, 16, 12), leather));
    this.fieldingGloveCover.scale.set(1.5, 1.08, .72);
    this.object.add(this.fieldingGloveCover);
    this.ownedGeometries.push(this.fieldingGloveCover.geometry);

    this.bat = shadow(new THREE.Mesh(new THREE.CylinderGeometry(.075, .12, 3.15, 14), wood));
    this.object.add(this.bat);
    this.ownedGeometries.push(this.bat.geometry);

    this.handCovers = ['LeftHand', 'RightHand'].map(name => {
      const cover = shadow(new THREE.Mesh(new THREE.SphereGeometry(.064, 12, 8), battingGlove));
      cover.scale.set(1.08, 1.38, .88);
      this.bones[name]?.add(cover);
      this.ownedGeometries.push(cover.geometry);
      return cover;
    });
    this.gripCovers = [0, 1].map(() => {
      const cover = shadow(new THREE.Mesh(new THREE.SphereGeometry(.12, 14, 10), battingGlove));
      cover.scale.set(.92, 1.34, .92);
      this.object.add(cover);
      this.ownedGeometries.push(cover.geometry);
      return cover;
    });

    this.mixer = new THREE.AnimationMixer(this.visual);
    this.actions = new Map();
    this.actionSources = new Map();
    for (const [name, source] of ANIMATION_CLIPS) {
      const adapted = adaptProceduralClip(source, this.bones);
      if (adapted.tracks.length) {
        this.actions.set(name, this.mixer.clipAction(adapted));
        this.actionSources.set(name, 'procedural');
      }
    }
    for (const [name, source] of meshyAssets.animations) {
      if (name === 'Pitch_R' || name === 'Run' || name === 'Walk') {
        this.actions.set(name, this.mixer.clipAction(sanitizeImportedClip(source)));
        this.actionSources.set(name, 'meshy');
      }
      if (name === 'Pitch_R') {
        this.actions.set('Pitch_L', this.mixer.clipAction(mirrorImportedClip(source, 'Pitch_L')));
        this.actionSources.set('Pitch_L', 'meshy-mirrored');
      }
    }

    this.currentName = '';
    this.currentSource = '';
    this.currentAction = null;
    this.setEquipment(this.role);
    this.play(this.role === 'Batter' ? `BatterIdle_${this.batHand}` : 'FieldReady');
  }

  resetPose() {
    for (const skeleton of this.skeletons) skeleton.pose();
  }

  setEquipment(role) {
    const batter = role === 'Batter';
    const runner = role === 'Runner';
    this.bat.visible = batter;
    this.glove.visible = !batter && !runner;
    this.fieldingGloveCover.visible = !batter && !runner;
    for (const cover of this.handCovers) cover.visible = batter;
    for (const cover of this.gripCovers) cover.visible = batter;
  }

  play(name, normalizedTime = null, loop = true) {
    if (!this.actions.has(name)) name = this.actions.has('FieldReady') ? 'FieldReady' : 'Walk';
    if (name !== this.currentName) {
      if (this.currentAction) this.currentAction.stop();
      this.resetPose();
      this.currentAction = this.actions.get(name);
      this.currentAction.reset();
      this.currentAction.enabled = true;
      this.currentAction.clampWhenFinished = !loop;
      this.currentAction.setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce, loop ? Infinity : 1);
      this.currentAction.play();
      this.currentName = name;
      this.currentSource = this.actionSources.get(name) || 'unknown';
    }
    if (normalizedTime != null) {
      const duration = this.currentAction.getClip().duration;
      this.currentAction.paused = true;
      this.currentAction.time = THREE.MathUtils.clamp(normalizedTime, 0, .9999) * duration;
      this.mixer.update(0);
    } else {
      this.currentAction.paused = false;
    }
  }

  update(deltaSeconds) {
    this.mixer.update(Math.min(.05, Math.max(0, deltaSeconds || 0)));
    this.updateBat();
    this.updateFieldingGlove();
  }

  updateFieldingGlove() {
    if (!this.fieldingGloveCover.visible) return;
    const gloveHand = this.throwHand === 'L' ? this.bones.RightHand : this.bones.LeftHand;
    if (!gloveHand) return;
    this.object.updateMatrixWorld(true);
    const position = gloveHand.getWorldPosition(new THREE.Vector3());
    this.object.worldToLocal(position);
    this.fieldingGloveCover.position.copy(position);
  }

  updateBat() {
    if (!this.bat.visible || !this.bones.LeftHand || !this.bones.RightHand) return;
    this.object.updateMatrixWorld(true);
    const left = this.bones.LeftHand.getWorldPosition(new THREE.Vector3());
    const right = this.bones.RightHand.getWorldPosition(new THREE.Vector3());
    this.object.worldToLocal(left);
    this.object.worldToLocal(right);
    const grip = left.clone().add(right).multiplyScalar(.5);
    const fallback = new THREE.Vector3(this.batHand === 'L' ? -.34 : .34, .93, -.14).normalize();
    const handAxis = right.clone().sub(left);
    let direction = fallback;
    if (handAxis.lengthSq() > .0025) {
      handAxis.normalize();
      if (handAxis.dot(fallback) < 0) handAxis.negate();
      direction = handAxis.lerp(fallback, .28).normalize();
    }
    this.bat.position.copy(grip).addScaledVector(direction, 1.38);
    this.bat.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction);
    this.gripCovers[0].position.copy(grip).addScaledVector(direction, -.085);
    this.gripCovers[1].position.copy(grip).addScaledVector(direction, .085);
    for (const cover of this.gripCovers)
      cover.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction);
  }

  dispose() {
    this.mixer.stopAllAction();
    this.mixer.uncacheRoot(this.visual);
    for (const geometry of this.ownedGeometries) geometry.dispose();
    for (const appearance of this.materials) appearance.dispose();
    for (const skeleton of this.skeletons) skeleton.dispose();
  }
}

export function createBaseballCharacter(options) {
  return meshyAssets
    ? new MeshyBaseballCharacterRig(options)
    : new BaseballCharacterRig(options);
}
