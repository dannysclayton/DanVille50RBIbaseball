import * as THREE from './three.module.min.js';
import { createBaseballCharacter, initializeBaseballCharacterAssets } from './baseball-character-rig.js';

const canvas = document.querySelector('#game');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance', preserveDrawingBuffer: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.05;

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x83b9e7);
scene.fog = new THREE.Fog(0x9cc6e6, 135, 255);

const camera = new THREE.PerspectiveCamera(42, 16 / 9, 0.1, 400);
const desiredCamera = new THREE.Vector3(-5.8, 6.3, 30);
const desiredTarget = new THREE.Vector3(0, 3.2, -7);
const cameraTarget = desiredTarget.clone();
camera.position.copy(desiredCamera);
camera.lookAt(cameraTarget);

scene.add(new THREE.HemisphereLight(0xdceeff, 0x315b2b, 2.0));
const sun = new THREE.DirectionalLight(0xfff4dc, 3.2);
sun.position.set(-48, 78, 35);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -95;
sun.shadow.camera.right = 95;
sun.shadow.camera.top = 80;
sun.shadow.camera.bottom = -115;
sun.shadow.camera.near = 1;
sun.shadow.camera.far = 240;
sun.shadow.bias = -0.0004;
scene.add(sun);

const mat = (color, roughness = 0.78, metalness = 0.02) =>
  new THREE.MeshStandardMaterial({ color, roughness, metalness });
const grass = mat(0x2f8440, 0.98);
const grassDark = mat(0x267237, 0.98);
const dirt = mat(0xb9824e, 1);
const clay = mat(0x9e6738, 1);
const chalk = new THREE.LineBasicMaterial({ color: 0xf8f7e8 });
const white = mat(0xf4f4ed, 0.65);
const wallMaterial = mat(0x214f3f, 0.82);
const standMaterial = mat(0x53606b, 0.9);
const seatMaterials = [mat(0x173a61, .85), mat(0x8e2428, .85), mat(0xd4d8db, .85)];
const scoreboardFaceMaterial = new THREE.MeshBasicMaterial({ color: 0x163d62 });
const fieldMaterials = { grass, grassDark, dirt, clay, wallMaterial, standMaterial, seatMaterials, scoreboardFaceMaterial };

function receive(mesh, cast = false) {
  mesh.receiveShadow = true;
  mesh.castShadow = cast;
  return mesh;
}

function createField() {
  const ground = receive(new THREE.Mesh(new THREE.PlaneGeometry(190, 210), grass));
  ground.rotation.x = -Math.PI / 2;
  ground.position.set(0, -0.04, -54);
  scene.add(ground);

  for (let i = -4; i <= 4; i++) {
    const stripe = receive(new THREE.Mesh(new THREE.PlaneGeometry(15, 205), i % 2 ? grassDark : grass));
    stripe.rotation.x = -Math.PI / 2;
    stripe.position.set(i * 18, 0, -54);
    scene.add(stripe);
  }

  const infieldShape = new THREE.Shape();
  infieldShape.moveTo(0, 20);
  infieldShape.lineTo(21, 0);
  infieldShape.lineTo(0, -21);
  infieldShape.lineTo(-21, 0);
  infieldShape.closePath();
  const infield = receive(new THREE.Mesh(new THREE.ShapeGeometry(infieldShape), dirt));
  infield.rotation.x = -Math.PI / 2;
  infield.position.y = 0.035;
  scene.add(infield);

  const mound = receive(new THREE.Mesh(new THREE.CylinderGeometry(3.5, 4.2, 0.28, 32), clay));
  mound.position.set(0, 0.1, -8);
  scene.add(mound);

  const homeCircle = receive(new THREE.Mesh(new THREE.CircleGeometry(5.7, 36), clay));
  homeCircle.rotation.x = -Math.PI / 2;
  homeCircle.position.set(0, 0.04, 18);
  scene.add(homeCircle);

  const bases = [[18, 0], [0, -18], [-18, 0]];
  for (const [x, z] of bases) {
    const base = receive(new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.22, 1.7), white), true);
    base.position.set(x, 0.2, z);
    base.rotation.y = Math.PI / 4;
    scene.add(base);
  }
  const plate = receive(new THREE.Mesh(new THREE.CylinderGeometry(1.15, 1.15, 0.12, 5), white));
  plate.position.set(0, 0.14, 18);
  plate.rotation.y = Math.PI;
  scene.add(plate);

  const points = [
    new THREE.Vector3(0, 0.12, 18),
    new THREE.Vector3(77, 0.12, -104),
    new THREE.Vector3(0, 0.12, 18),
    new THREE.Vector3(-77, 0.12, -104)
  ];
  const geometry = new THREE.BufferGeometry().setFromPoints(points);
  scene.add(new THREE.LineSegments(geometry, chalk));

  const batterBox = new THREE.BufferGeometry().setFromPoints([
    new THREE.Vector3(-4.6, .13, 14.4), new THREE.Vector3(-1.0, .13, 14.4),
    new THREE.Vector3(-1.0, .13, 20.2), new THREE.Vector3(-4.6, .13, 20.2),
    new THREE.Vector3(-4.6, .13, 20.2), new THREE.Vector3(-4.6, .13, 14.4),
    new THREE.Vector3(1.0, .13, 14.4), new THREE.Vector3(4.6, .13, 14.4),
    new THREE.Vector3(4.6, .13, 14.4), new THREE.Vector3(4.6, .13, 20.2),
    new THREE.Vector3(4.6, .13, 20.2), new THREE.Vector3(1.0, .13, 20.2)
  ]);
  scene.add(new THREE.LineSegments(batterBox, chalk));
}

function createStadium() {
  for (let i = 0; i < 42; i++) {
    const angle = THREE.MathUtils.lerp(-1.02, 1.02, i / 41);
    const radius = 118;
    const wall = receive(new THREE.Mesh(new THREE.BoxGeometry(7.2, 5.8, 1.7), wallMaterial), true);
    wall.position.set(Math.sin(angle) * radius, 2.9, -4 - Math.cos(angle) * radius);
    wall.rotation.y = -angle;
    scene.add(wall);
  }

  for (let tier = 0; tier < 3; tier++) {
    const radius = 128 + tier * 11;
    const height = 7 + tier * 5;
    for (let i = 0; i < 36; i++) {
      const angle = THREE.MathUtils.lerp(-1.08, 1.08, i / 35);
      const stand = receive(new THREE.Mesh(new THREE.BoxGeometry(9.8, 4.2, 7.8), standMaterial));
      stand.position.set(Math.sin(angle) * radius, height, -4 - Math.cos(angle) * radius);
      stand.rotation.y = -angle;
      scene.add(stand);
      const seats = new THREE.Mesh(new THREE.BoxGeometry(9.2, 0.65, 6.5), seatMaterials[(i + tier) % seatMaterials.length]);
      seats.position.copy(stand.position);
      seats.position.y += 2.35;
      seats.rotation.y = -angle;
      seats.rotation.x = -0.18;
      scene.add(seats);
    }
  }

  const board = receive(new THREE.Mesh(new THREE.BoxGeometry(31, 15, 2), mat(0x0a1926, .55, .25)), true);
  board.position.set(0, 16, -128);
  scene.add(board);
  const boardFace = new THREE.Mesh(new THREE.PlaneGeometry(27, 11), scoreboardFaceMaterial);
  boardFace.position.set(0, 16, -126.95);
  scene.add(boardFace);

  const poleMaterial = mat(0xd7d4b4, .55, .35);
  for (const x of [-68, -42, 42, 68]) {
    const pole = receive(new THREE.Mesh(new THREE.CylinderGeometry(.22, .3, 34, 10), poleMaterial), true);
    pole.position.set(x, 17, -105);
    scene.add(pole);
    const lights = new THREE.Mesh(new THREE.BoxGeometry(10, 4.5, .7), mat(0xe9ece8, .4, .2));
    lights.position.set(x, 33, -105);
    scene.add(lights);
  }
}

createField();
createStadium();

const ball = receive(new THREE.Mesh(new THREE.SphereGeometry(.22, 16, 12), white), true);
scene.add(ball);
const ballShadow = new THREE.Mesh(new THREE.CircleGeometry(.32, 16), new THREE.MeshBasicMaterial({ color: 0x111111, transparent: true, opacity: .42 }));
ballShadow.rotation.x = -Math.PI / 2;
scene.add(ballShadow);

let state = {
  phase: 'Ready', cameraPhase: 'AtBat', animationProgress: 0,
  presentationKind: 'None', presentationProgress: 0,
  presentationFromBase: 0, presentationTargetBase: 0, presentationSuccessful: false,
  presentationVariant: '',
  awayName: 'AWAY', homeName: 'HOME', awayScore: 0, homeScore: 0,
  inning: 1, topHalf: true, balls: 0, strikes: 0, outs: 0,
  modeLabel: 'Ready', pitchType: 'Fastball', batterName: 'Batter', pitcherName: 'Pitcher',
  offensePrimary: '#315fc4', offenseSecondary: '#eeeeee',
  offenseCap: '#315fc4', defensePrimary: '#c43131', defenseSecondary: '#ffffff', defenseCap: '#c43131',
  field: {
    name: 'Baseball Field', grass: '#2f8440', darkGrass: '#267237', infield: '#b9824e',
    clay: '#9e6738', wall: '#214f3f', seats: '#173a61', structure: '#53606b', accent: '#163d62'
  },
  scoreboard: {
    enabled: false, schoolName: 'HOME', abbreviation: 'HOME', mascot: '', layout: 'Solid',
    color1: '#113655', color2: '#113655', color3: '#071d34', color4: '#071d34',
    accent: '#cbe0ef', text: '#ffffff', adStrip: '#161616', logoDataUri: '', backgroundDataUri: '', ads: []
  },
  batterBats: 'R', pitcherThrows: 'R', batterTargetBase: 0, bases: [false, false, false],
  ball: { x: .5, y: .62, z: 0, visible: true },
  throwTarget: { x: .5, y: .58 },
  fielders: [
    { label: 'C', x: .5, y: .90 }, { label: 'P', x: .5, y: .62 },
    { label: '1B', x: .68, y: .70 }, { label: '2B', x: .59, y: .62 },
    { label: 'SS', x: .41, y: .62 }, { label: '3B', x: .32, y: .70 },
    { label: 'LF', x: .25, y: .38 }, { label: 'CF', x: .5, y: .28 },
    { label: 'RF', x: .75, y: .38 }
  ]
};
let batter;
let umpire;
let fielders = [];
let runners = [];
let lastOffenseColors = '';
let lastDefenseColors = '';
let lastFieldStyle = '';
let lastScoreboardStyle = '';
const characterTimer = new THREE.Timer();

function addCharacter(options) {
  const character = createBaseballCharacter(options);
  scene.add(character.object);
  return character;
}

function removeCharacter(character) {
  if (!character) return;
  scene.remove(character.object);
  character.dispose();
}

function rebuildPlayers() {
  removeCharacter(batter);
  removeCharacter(umpire);
  for (const player of fielders) removeCharacter(player);
  for (const player of runners) removeCharacter(player);
  fielders = [];
  runners = [];
  batter = addCharacter({
    primary: state.offensePrimary, secondary: state.offenseSecondary, cap: state.offenseCap,
    role: 'Batter', batHand: state.batterBats
  });
  umpire = addCharacter({
    primary: '#6faed9', secondary: '#20242a', cap: '#20242a',
    role: 'Runner', batHand: 'R'
  });
  for (const marker of state.fielders) {
    const player = addCharacter({
      primary: state.defensePrimary, secondary: state.defenseSecondary, cap: state.defenseCap,
      role: marker.label === 'C' ? 'Catcher' : marker.label === 'P' ? 'Pitcher' : 'Fielder',
      throwHand: marker.throws || (marker.label === 'P' ? state.pitcherThrows : 'R')
    });
    player.object.userData.label = marker.label;
    fielders.push(player);
  }
  for (let i = 0; i < 3; i++) {
    runners.push(addCharacter({
      primary: state.offensePrimary, secondary: state.offenseSecondary, cap: state.offenseCap,
      role: 'Runner', batHand: state.batterBats
    }));
  }
  lastOffenseColors = state.offensePrimary + state.offenseSecondary + state.offenseCap;
  lastDefenseColors = state.defensePrimary + state.defenseSecondary + state.defenseCap;
}

function worldPoint(x, y, height = 0) {
  const depth = y >= .58
    ? -13 + ((y - .58) / .28) * 31
    : -13 - ((.58 - y) / .50) * 115;
  return new THREE.Vector3((x - .5) * 150, .2 + height * 30, depth);
}

const baseWorld = [
  new THREE.Vector3(0, 0, 18),
  new THREE.Vector3(18, 0, 0),
  new THREE.Vector3(0, 0, -18),
  new THREE.Vector3(-18, 0, 0),
  new THREE.Vector3(0, 0, 18)
];

function pathToBase(targetBase, progress) {
  const target = THREE.MathUtils.clamp(Number(targetBase || 0), 0, 4);
  if (target === 0) return baseWorld[0].clone();
  const distance = THREE.MathUtils.clamp(progress, 0, 1) * target;
  const segment = Math.min(target - 1, Math.floor(distance));
  return baseWorld[segment].clone().lerp(baseWorld[segment + 1], distance - segment);
}

function quadraticPoint(start, control, end, progress) {
  const t = clamp01(progress);
  const inverse = 1 - t;
  return start.clone().multiplyScalar(inverse * inverse)
    .add(control.clone().multiplyScalar(2 * inverse * t))
    .add(end.clone().multiplyScalar(t * t));
}

function pathToDouble(progress) {
  const p = clamp01(progress);
  const firstApproach = baseWorld[0].clone().lerp(baseWorld[1], .86);
  const firstExit = baseWorld[1].clone().lerp(baseWorld[2], .18);
  const outsideTurn = baseWorld[1].clone().add(new THREE.Vector3(4.2, 0, -.7));
  if (p < .39)
    return baseWorld[0].clone().lerp(firstApproach, smoothStep(p / .39));
  if (p < .60)
    return quadraticPoint(firstApproach, outsideTurn, firstExit, smoothStep((p - .39) / .21));
  return firstExit.clone().lerp(baseWorld[2], smoothStep((p - .60) / .40));
}

function faceDirection(object, from, to) {
  object.rotation.y = Math.atan2(to.x - from.x, to.z - from.z);
}

const clamp01 = value => THREE.MathUtils.clamp(Number(value || 0), 0, 1);
const smoothStep = value => {
  const progress = clamp01(value);
  return progress * progress * (3 - 2 * progress);
};

function updateActors(deltaSeconds) {
  if (!batter || lastOffenseColors !== state.offensePrimary + state.offenseSecondary + state.offenseCap ||
      lastDefenseColors !== state.defensePrimary + state.defenseSecondary + state.defenseCap ||
      fielders.length !== state.fielders.length) rebuildPlayers();

  const left = String(state.batterBats).toUpperCase() === 'L';
  const progress = Number(state.animationProgress || 0);
  const steal = state.presentationKind === 'Steal';
  const stealProgress = clamp01(state.presentationProgress);
  const stealCaught = steal && !state.presentationSuccessful;
  const strikeout = state.presentationKind === 'Strikeout';
  const strikeoutProgress = clamp01(state.presentationProgress);
  const baseHit = state.presentationKind === 'BaseHit';
  const baseHitProgress = clamp01(state.presentationProgress);
  const centerChargeSingle = baseHit &&
    String(state.presentationVariant || '').toLowerCase() === 'centerchargesingle';
  const standingDouble = baseHit && Number(state.presentationTargetBase || 0) === 2;
  const swingingStrikeout = String(state.presentationVariant || '').toLowerCase() === 'swinging';
  if (strikeout) {
    const exitProgress = smoothStep((strikeoutProgress - .55) / .45);
    batter.object.position.set(left ? 3.65 : -3.65, 0, 16.55);
    batter.object.position.x += (left ? 1 : -1) * exitProgress * 7;
    batter.object.position.z += exitProgress * 3.5;
    batter.object.rotation.y = left ? -1.08 : 1.08;
    if (exitProgress > .02) {
      batter.setEquipment('Runner');
      batter.play('StrikeoutWalkAway', clamp01((strikeoutProgress - .55) / .45), false);
    } else {
      batter.setEquipment('Batter');
      batter.play(swingingStrikeout ? `StrikeoutReaction_${left ? 'L' : 'R'}` : 'CalledStrikeReaction',
        clamp01(strikeoutProgress / .58), false);
    }
  } else if (baseHit) {
    if (baseHitProgress < .14) {
      batter.object.position.set(left ? 3.65 : -3.65, 0, 16.55);
      batter.object.rotation.y = left ? -1.08 : 1.08;
      batter.setEquipment('Batter');
      batter.play(`Swing_${left ? 'L' : 'R'}`, clamp01(baseHitProgress / .14), false);
    } else {
      const runProgress = smoothStep((baseHitProgress - .12) /
        (standingDouble ? .76 : centerChargeSingle ? .67 : .64));
      const runPoint = standingDouble ? pathToDouble(runProgress) : pathToBase(1, runProgress);
      const ahead = standingDouble
        ? pathToDouble(Math.min(1, runProgress + .025))
        : pathToBase(1, Math.min(1, runProgress + .035));
      const firstLine = baseWorld[1].clone().sub(baseWorld[0]).normalize();
      const overrunStart = centerChargeSingle ? .79 : .76;
      const overrunReturn = centerChargeSingle ? .91 : .91;
      const overrun = smoothStep((baseHitProgress - overrunStart) / .12) *
        (1 - smoothStep((baseHitProgress - overrunReturn) / .08));
      if (!standingDouble)
        runPoint.addScaledVector(firstLine, overrun * (centerChargeSingle ? 3.8 : 3.2));
      batter.object.position.copy(runPoint);
      const doubleFinishDirection = baseWorld[2].clone().sub(baseWorld[1]).normalize();
      const facingTarget = standingDouble && runProgress >= .985
        ? baseWorld[2].clone().add(doubleFinishDirection)
        : overrun > .02 ? baseWorld[1].clone().add(firstLine) : ahead;
      faceDirection(batter.object, runPoint, facingTarget);
      batter.setEquipment('Runner');
      if (standingDouble && baseHitProgress >= .86)
        batter.play('RunnerStopAtSecond', clamp01((baseHitProgress - .86) / .14), false);
      else if (baseHitProgress < (centerChargeSingle ? .80 : .77) || standingDouble)
        batter.play('Run');
      else
        batter.play('RunnerBrakeAtFirst',
          clamp01((baseHitProgress - (centerChargeSingle ? .80 : .77)) / (centerChargeSingle ? .20 : .23)),
          false);
    }
  } else if (!steal && state.phase === 'BallInPlay' && Number(state.batterTargetBase || 0) > 0) {
    const runPoint = pathToBase(state.batterTargetBase, progress);
    const ahead = pathToBase(state.batterTargetBase, Math.min(1, progress + .03));
    batter.object.position.copy(runPoint);
    faceDirection(batter.object, runPoint, ahead);
    batter.setEquipment('Runner');
    batter.play('Run');
  } else {
    batter.object.position.set(left ? 3.65 : -3.65, 0, 16.55);
    batter.object.rotation.y = left ? -1.08 : 1.08;
    batter.setEquipment('Batter');
    const swinging = !steal && state.phase === 'Pitching' && progress >= .38;
    batter.play(swinging ? `Swing_${left ? 'L' : 'R'}` : `BatterIdle_${left ? 'L' : 'R'}`,
      swinging ? (progress - .38) / .62 : null, !swinging);
  }
  batter.object.scale.setScalar(1.1);
  batter.update(deltaSeconds);

  const relayTarget = worldPoint(state.throwTarget?.x ?? .5, state.throwTarget?.y ?? .58, 0);
  let relayReceiverIndex = -1;
  if (baseHit && (state.cameraPhase === 'ThrowToBase' || state.cameraPhase === 'ClosePlay')) {
    let bestDistance = Number.POSITIVE_INFINITY;
    state.fielders.forEach((marker, index) => {
      if (index === state.activeFielderIndex || marker.label === 'C' || marker.label === 'P') return;
      const distance = worldPoint(marker.x, marker.y, 0).distanceToSquared(relayTarget);
      if (distance < bestDistance) {
        bestDistance = distance;
        relayReceiverIndex = index;
      }
    });
  }

  state.fielders.forEach((marker, i) => {
    if (!fielders[i]) return;
    const player = fielders[i];
    let point = worldPoint(marker.x, marker.y, 0);
    const active = i === state.activeFielderIndex;
    if (steal && active && marker.label !== 'C' && marker.label !== 'P') {
      const fromBase = THREE.MathUtils.clamp(Number(state.presentationFromBase || 1), 1, 3);
      const targetBase = THREE.MathUtils.clamp(Number(state.presentationTargetBase || 2), 1, 4);
      const incoming = baseWorld[targetBase].clone().sub(baseWorld[fromBase]).normalize();
      const tagSide = new THREE.Vector3(-incoming.z, 0, incoming.x).multiplyScalar(.82);
      const tagSpot = baseWorld[targetBase].clone().add(tagSide);
      point = point.clone().lerp(tagSpot, smoothStep((stealProgress - .55) / .28));
    }
    if (baseHit && i === relayReceiverIndex) {
      const relayMoveProgress = state.cameraPhase === 'ClosePlay' ? 1 : progress;
      point = point.clone().lerp(relayTarget, smoothStep(relayMoveProgress * 1.18));
    }
    player.object.position.copy(point);
    player.object.visible = true;
    player.object.rotation.y = marker.label === 'C' ? Math.PI : 0;
    player.object.scale.setScalar(active ? 1.06 : 1);
    if (strikeout && marker.label === 'P') {
      player.play('PitcherStrikeoutReset', strikeoutProgress, false);
    } else if (strikeout && marker.label === 'C') {
      if (strikeoutProgress < .24)
        player.play('CatcherReceive', clamp01(strikeoutProgress / .24), false);
      else
        player.play('CatcherReceive', 1, false);
    } else if (steal && marker.label === 'P') {
      const throws = String(marker.throws || state.pitcherThrows || 'R').toUpperCase();
      if (stealProgress < .52)
        player.play(`Pitch_${throws === 'L' ? 'L' : 'R'}`, clamp01(stealProgress / .48), false);
      else
        player.play('FieldReady');
    } else if (steal && marker.label === 'C') {
      const throws = String(marker.throws || 'R').toUpperCase();
      if (stealProgress < .43) {
        player.play('CatcherCrouch');
      } else {
        player.play(`CatcherPopThrow_${throws === 'L' ? 'L' : 'R'}`,
          clamp01((stealProgress - .43) / .31), false);
      }
    } else if (steal && active) {
      if (stealProgress < .70) {
        player.play('FieldReady');
      } else if (stealProgress < .83) {
        player.play('Catch', clamp01((stealProgress - .70) / .13), false);
      } else {
        player.play('SweepTag', clamp01((stealProgress - .83) / .17), false);
      }
    } else if (baseHit && active && state.cameraPhase === 'BallTracking') {
      const pickupStart = standingDouble ? .55 : centerChargeSingle ? .58 : .53;
      if (baseHitProgress < pickupStart)
        player.play('Run');
      else
        player.play('FielderPickup', clamp01((baseHitProgress - pickupStart) / (.70 - pickupStart)), false);
      faceDirection(player.object, point, worldPoint(state.ball.x, state.ball.y, 0));
    } else if (baseHit && active && state.cameraPhase === 'ThrowToBase') {
      const throws = String(marker.throws || 'R').toUpperCase();
      player.play(`Throw_${throws === 'L' ? 'L' : 'R'}`, progress, false);
      faceDirection(player.object, point, relayTarget);
    } else if (baseHit && i === relayReceiverIndex) {
      player.play('RelayReceive', state.cameraPhase === 'ClosePlay' ? 1 : progress, false);
      faceDirection(player.object, point, worldPoint(state.ball.x, state.ball.y, 0));
    } else if (centerChargeSingle && baseHit && active && state.cameraPhase === 'ClosePlay') {
      player.play('FieldReady');
      faceDirection(player.object, point, relayTarget);
    } else if (marker.label === 'P' && state.phase === 'Pitching') {
      const throws = String(marker.throws || state.pitcherThrows || 'R').toUpperCase();
      player.play(`Pitch_${throws === 'L' ? 'L' : 'R'}`, progress, false);
    } else if (active && state.cameraPhase === 'ThrowToBase') {
      const throws = String(marker.throws || 'R').toUpperCase();
      player.play(`Throw_${throws === 'L' ? 'L' : 'R'}`, progress, false);
    } else if (active && state.cameraPhase === 'ClosePlay') {
      player.play('Catch', progress, false);
    } else if (active && state.phase === 'BallInPlay') {
      player.play('Run');
      faceDirection(player.object, point, worldPoint(state.ball.x, state.ball.y, 0));
    } else if (marker.label === 'C') {
      player.play('CatcherCrouch');
    } else {
      player.play('FieldReady');
    }
    player.update(deltaSeconds);
  });

  const safeCallStart = standingDouble ? .92 : centerChargeSingle ? .90 : .82;
  const baseHitSafeCall = baseHit && baseHitProgress >= safeCallStart;
  const stealCallStart = stealCaught ? .88 : .90;
  const stealBaseCall = steal && stealProgress >= stealCallStart;
  umpire.object.visible = baseHitSafeCall || stealBaseCall ||
    (!steal && state.cameraPhase === 'AtBat' && state.phase !== 'BallInPlay');
  if (umpire.object.visible) {
    if (baseHitSafeCall) {
      const callBase = standingDouble ? baseWorld[2] : baseWorld[1];
      const previousBase = standingDouble ? baseWorld[1] : baseWorld[0];
      const towardRunner = previousBase.clone().sub(callBase).normalize();
      const umpireSide = new THREE.Vector3(towardRunner.z, 0, -towardRunner.x).multiplyScalar(5.2);
      umpire.object.position.copy(callBase).addScaledVector(towardRunner, 4.6).add(umpireSide);
      faceDirection(umpire.object, umpire.object.position, callBase);
    } else if (stealBaseCall) {
      const fromBase = THREE.MathUtils.clamp(Number(state.presentationFromBase || 1), 1, 3);
      const targetBase = THREE.MathUtils.clamp(Number(state.presentationTargetBase || 2), 1, 4);
      const towardRunner = baseWorld[fromBase].clone().sub(baseWorld[targetBase]).normalize();
      const umpireSide = new THREE.Vector3(towardRunner.z, 0, -towardRunner.x).multiplyScalar(5.2);
      umpire.object.position.copy(baseWorld[targetBase]).addScaledVector(towardRunner, 4.8).add(umpireSide);
      faceDirection(umpire.object, umpire.object.position, baseWorld[targetBase]);
    } else {
      umpire.object.position.set(.9, 0, 24.1);
      umpire.object.rotation.y = Math.PI;
    }
    umpire.object.scale.setScalar(.8);
    umpire.setEquipment('Runner');
    if (baseHitSafeCall)
      umpire.play('UmpireSafe', clamp01((baseHitProgress - safeCallStart) / (1 - safeCallStart)), false);
    else if (stealBaseCall)
      umpire.play(stealCaught ? 'UmpireOut' : 'UmpireSafe',
        clamp01((stealProgress - stealCallStart) / (1 - stealCallStart)), false);
    else if (strikeout)
      umpire.play('UmpireStrikeout', clamp01((strikeoutProgress - .12) / .72), false);
    else
      umpire.play('UmpireSet');
    umpire.update(deltaSeconds);
  }

  const basePositions = [baseWorld[1], baseWorld[2], baseWorld[3]];
  runners.forEach((runner, i) => {
    const presentationRunner = steal && i === Number(state.presentationFromBase || 0) - 1;
    runner.object.visible = presentationRunner || !!state.bases[i];
    if (!runner.object.visible) return;
    let position = basePositions[i];
    let next = baseWorld[i + 2];
    if (presentationRunner) {
      const fromBase = THREE.MathUtils.clamp(Number(state.presentationFromBase || 1), 1, 3);
      const targetBase = THREE.MathUtils.clamp(Number(state.presentationTargetBase || fromBase + 1), 1, 4);
      const runProgress = smoothStep((stealProgress - .06) / .78);
      const caughtStealingStop = stealCaught
        ? baseWorld[targetBase].clone().lerp(baseWorld[fromBase], .065)
        : baseWorld[targetBase];
      position = baseWorld[fromBase].clone().lerp(caughtStealingStop, runProgress);
      next = caughtStealingStop;
      if (runProgress < .78)
        runner.play('Run');
      else {
        const slideProgress = clamp01((runProgress - .78) / .22);
        runner.play('Slide', slideProgress, false);
        position.y -= .72 * slideProgress;
      }
    } else {
      runner.play('RunnerLead');
    }
    runner.object.position.copy(position);
    faceDirection(runner.object, position, next);
    runner.object.scale.setScalar(.97);
    runner.update(deltaSeconds);
  });

  const bp = worldPoint(state.ball.x, state.ball.y, state.ball.z);
  ball.position.copy(bp);
  ball.visible = !!state.ball.visible;
  ballShadow.position.set(bp.x, .16, bp.z);
  ballShadow.scale.setScalar(1 + Number(state.ball.z || 0) * 2);
  ballShadow.visible = ball.visible;
}

function updateCamera() {
  const bp = worldPoint(state.ball.x, state.ball.y, state.ball.z);
  const steal = state.presentationKind === 'Steal';
  const stealProgress = clamp01(state.presentationProgress);
  const stealCaught = steal && !state.presentationSuccessful;
  const strikeout = state.presentationKind === 'Strikeout';
  const baseHit = state.presentationKind === 'BaseHit';
  const baseHitProgress = clamp01(state.presentationProgress);
  const centerChargeSingle = baseHit &&
    String(state.presentationVariant || '').toLowerCase() === 'centerchargesingle';
  const standingDouble = baseHit && Number(state.presentationTargetBase || 0) === 2;
  if (strikeout) {
    desiredCamera.set(0, 5.55, 34.5);
    desiredTarget.set(0, 2.8, -6.5);
  } else if (baseHit && baseHitProgress < .14) {
    desiredCamera.set(0, 6.15, 31.5);
    desiredTarget.set(0, 3.0, -8.5);
  } else if (centerChargeSingle && baseHitProgress < .70) {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .34, Math.max(10.5, bp.y + 11.5), bp.z + 24);
  } else if (standingDouble && baseHitProgress >= .52 && baseHitProgress < .70) {
    const runProgress = smoothStep((baseHitProgress - .12) / .76);
    const runnerPoint = pathToDouble(runProgress);
    desiredTarget.copy(runnerPoint).lerp(bp, .14);
    desiredTarget.y = 1.45;
    desiredCamera.set(runnerPoint.x + 17, 8.2, runnerPoint.z + 24);
  } else if (baseHit && baseHitProgress < .70) {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .42, Math.max(13, bp.y + 15), bp.z + 31);
  } else if (centerChargeSingle && baseHitProgress < .90) {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .46 + 4.5, Math.max(9.5, bp.y + 10), bp.z + 21);
  } else if (baseHit && baseHitProgress < .90) {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .58 + 7, Math.max(10, bp.y + 11), bp.z + 23);
  } else if (standingDouble) {
    const aspect = canvas.clientWidth / Math.max(1, canvas.clientHeight);
    const narrowViewPullback = Math.max(1, 1.25 / Math.max(.65, aspect));
    desiredTarget.copy(baseWorld[2]);
    desiredTarget.y = 1.45;
    desiredCamera.set(
      baseWorld[2].x + 12 * narrowViewPullback,
      7.2 + 1.2 * (narrowViewPullback - 1),
      baseWorld[2].z - 20 * narrowViewPullback);
  } else if (baseHit) {
    const aspect = canvas.clientWidth / Math.max(1, canvas.clientHeight);
    const narrowViewPullback = Math.max(1, 1.25 / Math.max(.65, aspect));
    desiredTarget.copy(baseWorld[1]);
    desiredTarget.y = 1.6;
    desiredCamera.set(
      baseWorld[1].x + 12 * narrowViewPullback,
      6.8 + 1.1 * (narrowViewPullback - 1),
      baseWorld[1].z + 16 * narrowViewPullback);
  } else if (steal && stealProgress < .60) {
    desiredCamera.set(0, 7.1, 34);
    desiredTarget.set(0, 3.0, -6);
  } else if (steal && stealProgress < (stealCaught ? .78 : .84)) {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .22, 9, bp.z + 37);
  } else if (steal) {
    const fromBase = THREE.MathUtils.clamp(Number(state.presentationFromBase || 1), 1, 3);
    const targetBase = THREE.MathUtils.clamp(Number(state.presentationTargetBase || 2), 1, 4);
    const towardRunner = baseWorld[fromBase].clone().sub(baseWorld[targetBase]).normalize();
    const cameraSide = new THREE.Vector3(towardRunner.z, 0, -towardRunner.x);
    desiredTarget.copy(baseWorld[targetBase]);
    desiredTarget.y = stealCaught ? 1.35 : 1.1;
    desiredCamera.copy(baseWorld[targetBase])
      .addScaledVector(towardRunner, stealCaught ? 27 : 30)
      .addScaledVector(cameraSide, stealCaught ? 7.5 : 9);
    desiredCamera.y = stealCaught ? 8.2 : 8.8;
  } else if (state.cameraPhase === 'BallTracking') {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .55, Math.max(18, bp.y + 18), bp.z + 34);
  } else if (state.cameraPhase === 'ThrowToBase') {
    desiredTarget.copy(bp);
    desiredCamera.set(bp.x * .7 + 9, Math.max(12, bp.y + 12), bp.z + 25);
  } else if (state.cameraPhase === 'ClosePlay') {
    desiredTarget.copy(bp);
    desiredTarget.y = 2;
    desiredCamera.set(bp.x + 11, 7.5, bp.z + 18);
  } else {
    const left = String(state.batterBats).toUpperCase() === 'L';
    desiredCamera.set(0, 6.15, 31.5);
    desiredTarget.set(0, 3.0, -8.5);
  }
  camera.position.lerp(desiredCamera, .085);
  cameraTarget.lerp(desiredTarget, .10);
  camera.lookAt(cameraTarget);
}

function applyFieldPreset() {
  const next = state.field || {};
  const signature = JSON.stringify(next);
  if (signature === lastFieldStyle) return;
  lastFieldStyle = signature;

  const setColor = (material, value) => {
    if (material && value) material.color.set(value);
  };
  setColor(fieldMaterials.grass, next.grass);
  setColor(fieldMaterials.grassDark, next.darkGrass);
  setColor(fieldMaterials.dirt, next.infield);
  setColor(fieldMaterials.clay, next.clay);
  setColor(fieldMaterials.wallMaterial, next.wall);
  setColor(fieldMaterials.standMaterial, next.structure);
  setColor(fieldMaterials.seatMaterials[0], next.seats);
  setColor(fieldMaterials.seatMaterials[1], next.accent);
  setColor(fieldMaterials.seatMaterials[2], next.structure);
  setColor(fieldMaterials.scoreboardFaceMaterial, next.accent);
  text('venue', next.name || 'Baseball Field');
}

function scoreboardBackground(board) {
  const first = board.color1 || '#113655';
  const second = board.color2 || first;
  const third = board.color3 || first;
  const fourth = board.color4 || second;
  switch (board.layout) {
    case 'VerticalHalves': return `linear-gradient(90deg, ${first} 0 50%, ${second} 50% 100%)`;
    case 'HorizontalHalves': return `linear-gradient(180deg, ${first} 0 50%, ${second} 50% 100%)`;
    case 'Quarters': return `conic-gradient(from 0deg, ${first} 0 25%, ${second} 25% 50%, ${fourth} 50% 75%, ${third} 75% 100%)`;
    default: return first;
  }
}

function applyScoreboard() {
  const board = state.scoreboard || {};
  const signature = JSON.stringify(board) + ':' + state.inning;
  if (signature === lastScoreboardStyle) return;
  lastScoreboardStyle = signature;

  const score = document.querySelector('#score');
  const custom = !!board.enabled;
  score.classList.toggle('custom', custom);
  const boardColors = scoreboardBackground(board);
  score.style.background = custom && board.backgroundDataUri
    ? `linear-gradient(rgba(0,0,0,.28), rgba(0,0,0,.28)), url("${board.backgroundDataUri}") center / cover no-repeat, ${boardColors}`
    : custom ? boardColors : '';
  score.style.setProperty('--board-accent', board.accent || '#cbe0ef');
  score.style.setProperty('--board-text', board.text || '#ffffff');
  score.style.setProperty('--board-ad', board.adStrip || '#161616');
  score.style.color = custom ? (board.text || '#ffffff') : '';

  const identity = [board.schoolName, board.mascot].filter(Boolean).join(' ');
  text('boardIdentityText', identity || state.homeName);
  const logo = document.querySelector('#boardLogo');
  const hasLogo = custom && !!board.logoDataUri;
  logo.classList.toggle('visible', hasLogo);
  if (hasLogo && logo.src !== board.logoDataUri) logo.src = board.logoDataUri;
  if (!hasLogo) logo.removeAttribute('src');
  const ads = Array.isArray(board.ads) ? board.ads.filter(Boolean) : [];
  text('boardAd', ads.length ? ads[Math.max(0, Number(state.inning || 1) - 1) % ads.length] : 'HOME FIELD');
}

function updateHud() {
  text('awayName', state.awayName);
  text('homeName', state.scoreboard?.enabled && state.scoreboard.abbreviation
    ? state.scoreboard.abbreviation : state.homeName);
  text('awayRuns', state.awayScore);
  text('homeRuns', state.homeScore);
  text('pitchName', String(state.pitchType || 'Fastball').toUpperCase());
  text('matchup', state.pitcherName + ' vs ' + state.batterName);
  text('inningText', (state.topHalf ? 'TOP ' : 'BOT ') + state.inning);
  text('count', 'B ' + state.balls + '   S ' + state.strikes + '   O ' + state.outs);
  text('result', String(state.modeLabel || 'Ready').toUpperCase());
  for (let i = 0; i < 3; i++) document.querySelector('#b' + (i + 1)).classList.toggle('on', !!state.bases[i]);
  applyFieldPreset();
  applyScoreboard();
}

function text(id, value) {
  document.querySelector('#' + id).textContent = value == null ? '' : String(value);
}

window.DRBI = {
  updateState(next) {
    const scoreboardUpdate = { ...(next.scoreboard || {}) };
    if (scoreboardUpdate.logoDataUri == null) delete scoreboardUpdate.logoDataUri;
    if (scoreboardUpdate.backgroundDataUri == null) delete scoreboardUpdate.backgroundDataUri;
    state = {
      ...state,
      ...next,
      ball: { ...state.ball, ...(next.ball || {}) },
      throwTarget: { ...state.throwTarget, ...(next.throwTarget || {}) },
      field: { ...state.field, ...(next.field || {}) },
      scoreboard: { ...state.scoreboard, ...scoreboardUpdate }
    };
    state.fielders = Array.isArray(next.fielders) ? next.fielders : state.fielders;
    updateHud();
  },
  setTactical(enabled) {
    state.cameraPhase = enabled ? 'BallTracking' : 'AtBat';
  },
  getDebugState() {
    const characterBounds = character => {
      if (!character) return null;
      const bounds = new THREE.Box3().setFromObject(character.object);
      return {
        min: bounds.min.toArray(), max: bounds.max.toArray(),
        position: character.object.position.toArray(),
        scale: character.object.scale.toArray(),
        visible: character.object.visible,
        animation: character.currentName,
        animationSource: character.currentSource
      };
    };
    return {
      camera: camera.position.toArray(), target: cameraTarget.toArray(),
      batter: characterBounds(batter),
      fielders: fielders.map(characterBounds),
      runners: runners.map(characterBounds),
      umpire: characterBounds(umpire),
      sceneChildren: scene.children.length
    };
  }
};

function resize() {
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  if (canvas.width !== Math.floor(width * renderer.getPixelRatio()) ||
      canvas.height !== Math.floor(height * renderer.getPixelRatio())) {
    renderer.setSize(width, height, false);
    camera.aspect = width / Math.max(1, height);
    camera.updateProjectionMatrix();
  }
}

function render() {
  characterTimer.update();
  const deltaSeconds = characterTimer.getDelta();
  resize();
  updateActors(deltaSeconds);
  updateCamera();
  renderer.render(scene, camera);
  requestAnimationFrame(render);
}

async function bootstrap() {
  await initializeBaseballCharacterAssets();
  updateHud();
  rebuildPlayers();
  requestAnimationFrame(render);
  window.chrome?.webview?.postMessage({ type: 'ready' });
}

bootstrap().catch(error => {
  console.error('Gameplay renderer initialization failed.', error);
  document.querySelector('#fallback').style.display = 'grid';
  window.chrome?.webview?.postMessage({ type: 'renderer-error', message: String(error) });
});
