using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public enum GameplayPitchType
    {
        Fastball,
        Curveball,
        Slider,
        Changeup,
        Splitter,
        Forkball,
        Knuckleball
    }

    public enum GameplayBase
    {
        Home,
        First,
        Second,
        Third
    }

    public enum GameplayInputCommand
    {
        BatSwing,
        BatContactSwing,
        BatPowerSwing,
        PitchRelease,
        SelectFastball,
        SelectCurveball,
        SelectSlider,
        SelectChangeup,
        SelectSplitter,
        SelectForkball,
        SelectKnuckleball,
        ThrowHome,
        ThrowFirst,
        ThrowSecond,
        ThrowThird,
        AdvanceRunners,
        RetreatRunners,
        CallSacrificeBunt,
        CallHitAndRun,
        CallSteal,
        CallSafe,
        CallBunt,
        CallDoubleSteal,
        CallNormalDefense,
        CallInfieldIn,
        CallDoublePlay,
        CallOutfieldIn,
        CallNoDoubles,
        CallWheelPlay,
        CallIntentionalWalk,
        CallMoundVisit,
        ChangePitcher,
        TogglePause,
        ToggleCamera,
        ToggleWatch
    }

    public enum GameplayInputSource
    {
        Keyboard,
        Controller
    }

    public readonly struct GameplayInputEvent
    {
        public GameplayInputEvent(GameplayInputCommand command, GameplayInputSource source)
        {
            Command = command;
            Source = source;
        }

        public GameplayInputCommand Command { get; }
        public GameplayInputSource Source { get; }
    }

    public readonly struct GameplayDirection
    {
        public GameplayDirection(int x, int y)
        {
            X = Math.Sign(x);
            Y = Math.Sign(y);
        }

        public int X { get; }

        public int Y { get; }

        public bool IsNeutral => X == 0 && Y == 0;
    }

    public sealed class GameplayInputBindings
    {
        public static GameplayInputBindings Default { get; } = new GameplayInputBindings();

        public IReadOnlyList<Keys> MoveLeft { get; init; } = new[] { Keys.Left, Keys.A };

        public IReadOnlyList<Keys> MoveRight { get; init; } = new[] { Keys.Right, Keys.D };

        public IReadOnlyList<Keys> MoveUp { get; init; } = new[] { Keys.Up, Keys.W };

        public IReadOnlyList<Keys> MoveDown { get; init; } = new[] { Keys.Down, Keys.S };

        public IReadOnlyList<Keys> AimLeft { get; init; } = new[] { Keys.Left, Keys.A };

        public IReadOnlyList<Keys> AimRight { get; init; } = new[] { Keys.Right, Keys.D };

        public IReadOnlyList<Keys> AimUp { get; init; } = new[] { Keys.Up, Keys.W };

        public IReadOnlyList<Keys> AimDown { get; init; } = new[] { Keys.Down, Keys.S };

        public IReadOnlyList<Keys> BatSwing { get; init; } = new[] { Keys.Space };

        public IReadOnlyList<Keys> BatContactSwing { get; init; } = new[] { Keys.Z };

        public IReadOnlyList<Keys> BatPowerSwing { get; init; } = new[] { Keys.X };

        public IReadOnlyList<Keys> PitchRelease { get; init; } = new[] { Keys.Space };

        public IReadOnlyList<Keys> PitchFastball { get; init; } = new[] { Keys.D1 };

        public IReadOnlyList<Keys> PitchCurveball { get; init; } = new[] { Keys.D2 };

        public IReadOnlyList<Keys> PitchSlider { get; init; } = new[] { Keys.D3 };

        public IReadOnlyList<Keys> PitchChangeup { get; init; } = new[] { Keys.D4 };

        public IReadOnlyList<Keys> PitchSplitter { get; init; } = new[] { Keys.D5 };

        public IReadOnlyList<Keys> PitchForkball { get; init; } = new[] { Keys.F6 };

        public IReadOnlyList<Keys> PitchKnuckleball { get; init; } = new[] { Keys.F7 };

        public IReadOnlyList<Keys> ThrowHome { get; init; } = new[] { Keys.NumPad0, Keys.H };

        public IReadOnlyList<Keys> ThrowFirst { get; init; } = new[] { Keys.NumPad1, Keys.F };

        public IReadOnlyList<Keys> ThrowSecond { get; init; } = new[] { Keys.NumPad2, Keys.R };

        public IReadOnlyList<Keys> ThrowThird { get; init; } = new[] { Keys.NumPad3, Keys.T };

        public IReadOnlyList<Keys> AdvanceRunners { get; init; } = new[] { Keys.E };

        public IReadOnlyList<Keys> RetreatRunners { get; init; } = new[] { Keys.Q };

        public IReadOnlyList<Keys> SacrificeBunt { get; init; } = new[] { Keys.U };

        public IReadOnlyList<Keys> HitAndRun { get; init; } = new[] { Keys.N };

        public IReadOnlyList<Keys> Steal { get; init; } = new[] { Keys.G };

        public IReadOnlyList<Keys> Safe { get; init; } = new[] { Keys.Y };

        public IReadOnlyList<Keys> Bunt { get; init; } = new[] { Keys.J };

        public IReadOnlyList<Keys> DoubleSteal { get; init; } = new[] { Keys.K };

        public IReadOnlyList<Keys> NormalDefense { get; init; } = new[] { Keys.D0 };

        public IReadOnlyList<Keys> InfieldIn { get; init; } = new[] { Keys.I };

        public IReadOnlyList<Keys> DoublePlayDefense { get; init; } = new[] { Keys.D6 };

        public IReadOnlyList<Keys> OutfieldIn { get; init; } = new[] { Keys.D7 };

        public IReadOnlyList<Keys> NoDoubles { get; init; } = new[] { Keys.D8 };

        public IReadOnlyList<Keys> WheelPlay { get; init; } = new[] { Keys.O };

        public IReadOnlyList<Keys> IntentionalWalk { get; init; } = new[] { Keys.D9 };

        public IReadOnlyList<Keys> MoundVisit { get; init; } = new[] { Keys.M };

        public IReadOnlyList<Keys> ChangePitcher { get; init; } = new[] { Keys.B };

        public IReadOnlyList<Keys> Pause { get; init; } = new[] { Keys.Escape, Keys.P };

        public IReadOnlyList<Keys> CameraToggle { get; init; } = new[] { Keys.C };

        public IReadOnlyList<Keys> WatchToggle { get; init; } = new[] { Keys.Tab, Keys.V };
    }

    public readonly struct GameplayInputSnapshot
    {
        public GameplayInputSnapshot(
            GameplayDirection fielderMove,
            GameplayDirection pitchAim,
            bool batSwingHeld,
            bool batContactSwingHeld,
            bool batPowerSwingHeld,
            bool pitchReleaseHeld,
            GameplayPitchType selectedPitchType,
            bool advanceRunnersHeld,
            bool retreatRunnersHeld)
        {
            FielderMove = fielderMove;
            PitchAim = pitchAim;
            BatSwingHeld = batSwingHeld;
            BatContactSwingHeld = batContactSwingHeld;
            BatPowerSwingHeld = batPowerSwingHeld;
            PitchReleaseHeld = pitchReleaseHeld;
            SelectedPitchType = selectedPitchType;
            AdvanceRunnersHeld = advanceRunnersHeld;
            RetreatRunnersHeld = retreatRunnersHeld;
        }

        public GameplayDirection FielderMove { get; }

        public GameplayDirection PitchAim { get; }

        public bool BatSwingHeld { get; }

        public bool BatContactSwingHeld { get; }

        public bool BatPowerSwingHeld { get; }

        public bool PitchReleaseHeld { get; }

        public GameplayPitchType SelectedPitchType { get; }

        public bool AdvanceRunnersHeld { get; }

        public bool RetreatRunnersHeld { get; }
    }

    public sealed class GameplayInput
    {
        private readonly GameplayInputBindings _bindings;
        private readonly XInputGameplayControllerInput _controllerInput = new XInputGameplayControllerInput();
        private readonly HashSet<Keys> _downKeys = new HashSet<Keys>();
        private readonly List<GameplayInputCommand> _pendingKeyboardCommands = new List<GameplayInputCommand>();
        private readonly List<GameplayInputCommand> _pendingControllerCommands = new List<GameplayInputCommand>();

        public GameplayInput()
            : this(GameplayInputBindings.Default)
        {
        }

        public GameplayInput(GameplayInputBindings bindings)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            SelectedPitchType = GameplayPitchType.Fastball;
            KeyboardSelectedPitchType = GameplayPitchType.Fastball;
        }

        public bool ControllerEnabled { get; set; } = true;

        public int? ConnectedControllerIndex => ControllerEnabled ? _controllerInput.ConnectedControllerIndex : null;
        public string? ConnectedControllerId => ControllerEnabled ? _controllerInput.ConnectedControllerId : null;
        public string? ConnectedControllerName => ControllerEnabled ? _controllerInput.ConnectedControllerName : null;

        public int PreferredControllerIndex
        {
            get => _controllerInput.PreferredControllerIndex;
            set => _controllerInput.PreferredControllerIndex = value;
        }

        public int ControllerLeftStickDeadzone
        {
            get => _controllerInput.LeftStickDeadzone;
            set => _controllerInput.LeftStickDeadzone = value;
        }

        public GameplayPitchType SelectedPitchType { get; private set; }

        public GameplayPitchType KeyboardSelectedPitchType { get; private set; }

        public GameplayDirection FielderMove => CombineDirections(
            DirectionFrom(
                _bindings.MoveLeft,
                _bindings.MoveRight,
                _bindings.MoveUp,
                _bindings.MoveDown),
            ControllerEnabled ? _controllerInput.LeftStickDirection : default);

        public GameplayDirection PitchAim => CombineDirections(
            DirectionFrom(
                _bindings.AimLeft,
                _bindings.AimRight,
                _bindings.AimUp,
                _bindings.AimDown),
            ControllerEnabled ? _controllerInput.LeftStickDirection : default);

        public bool BatSwingHeld => AnyDown(_bindings.BatSwing) ||
            (ControllerEnabled && _controllerInput.BatSwingHeld);

        public bool BatContactSwingHeld => AnyDown(_bindings.BatContactSwing) ||
            (ControllerEnabled && _controllerInput.BatContactSwingHeld);

        public bool BatPowerSwingHeld => AnyDown(_bindings.BatPowerSwing) ||
            (ControllerEnabled && _controllerInput.BatPowerSwingHeld);

        public bool PitchReleaseHeld => AnyDown(_bindings.PitchRelease) ||
            (ControllerEnabled && _controllerInput.PitchReleaseHeld);

        public bool AdvanceRunnersHeld => AnyDown(_bindings.AdvanceRunners) ||
            (ControllerEnabled && _controllerInput.AdvanceRunnersHeld);

        public bool RetreatRunnersHeld => AnyDown(_bindings.RetreatRunners) ||
            (ControllerEnabled && _controllerInput.RetreatRunnersHeld);

        public GameplayInputSnapshot Snapshot => new GameplayInputSnapshot(
            FielderMove,
            PitchAim,
            BatSwingHeld,
            BatContactSwingHeld,
            BatPowerSwingHeld,
            PitchReleaseHeld,
            SelectedPitchType,
            AdvanceRunnersHeld,
            RetreatRunnersHeld);

        public GameplayInputSnapshot KeyboardSnapshot => new GameplayInputSnapshot(
            DirectionFrom(_bindings.MoveLeft, _bindings.MoveRight, _bindings.MoveUp, _bindings.MoveDown),
            DirectionFrom(_bindings.AimLeft, _bindings.AimRight, _bindings.AimUp, _bindings.AimDown),
            AnyDown(_bindings.BatSwing),
            AnyDown(_bindings.BatContactSwing),
            AnyDown(_bindings.BatPowerSwing),
            AnyDown(_bindings.PitchRelease),
            KeyboardSelectedPitchType,
            AnyDown(_bindings.AdvanceRunners),
            AnyDown(_bindings.RetreatRunners));

        public GameplayInputSnapshot ControllerSnapshot => new GameplayInputSnapshot(
            ControllerEnabled ? _controllerInput.LeftStickDirection : default,
            ControllerEnabled ? _controllerInput.LeftStickDirection : default,
            ControllerEnabled && _controllerInput.BatSwingHeld,
            ControllerEnabled && _controllerInput.BatContactSwingHeld,
            ControllerEnabled && _controllerInput.BatPowerSwingHeld,
            ControllerEnabled && _controllerInput.PitchReleaseHeld,
            _controllerInput.SelectedPitchType,
            ControllerEnabled && _controllerInput.AdvanceRunnersHeld,
            ControllerEnabled && _controllerInput.RetreatRunnersHeld);

        public bool HandleKeyDown(Keys key)
        {
            var normalizedKey = NormalizeKey(key);
            if (!_downKeys.Add(normalizedKey))
            {
                return false;
            }

            AddCommandsForKeyDown(normalizedKey);
            return true;
        }

        public bool HandleKeyUp(Keys key)
        {
            return _downKeys.Remove(NormalizeKey(key));
        }

        public bool IsKeyDown(Keys key)
        {
            return _downKeys.Contains(NormalizeKey(key));
        }

        public bool PollController()
        {
            if (!ControllerEnabled)
            {
                _controllerInput.Reset();
                return false;
            }

            var wasConnected = _controllerInput.Poll(_pendingControllerCommands);
            if (_controllerInput.SelectedPitchTypeChanged)
            {
                SelectedPitchType = _controllerInput.SelectedPitchType;
            }

            return wasConnected;
        }

        public IReadOnlyList<GameplayInputEvent> DrainCommandEvents()
        {
            if (_pendingKeyboardCommands.Count == 0 && _pendingControllerCommands.Count == 0)
            {
                return Array.Empty<GameplayInputEvent>();
            }

            var events = new List<GameplayInputEvent>(_pendingKeyboardCommands.Count + _pendingControllerCommands.Count);
            events.AddRange(_pendingKeyboardCommands.ConvertAll(c => new GameplayInputEvent(c, GameplayInputSource.Keyboard)));
            events.AddRange(_pendingControllerCommands.ConvertAll(c => new GameplayInputEvent(c, GameplayInputSource.Controller)));
            _pendingKeyboardCommands.Clear();
            _pendingControllerCommands.Clear();
            return events;
        }

        public void ClearCommands()
        {
            _pendingKeyboardCommands.Clear();
            _pendingControllerCommands.Clear();
        }

        public void Reset()
        {
            _downKeys.Clear();
            _pendingKeyboardCommands.Clear();
            _pendingControllerCommands.Clear();
            _controllerInput.Reset();
            SelectedPitchType = GameplayPitchType.Fastball;
            KeyboardSelectedPitchType = GameplayPitchType.Fastball;
        }

        private void AddCommandsForKeyDown(Keys key)
        {
            AddIfMatches(key, _bindings.BatSwing, GameplayInputCommand.BatSwing);
            AddIfMatches(key, _bindings.BatContactSwing, GameplayInputCommand.BatContactSwing);
            AddIfMatches(key, _bindings.BatPowerSwing, GameplayInputCommand.BatPowerSwing);
            AddIfMatches(key, _bindings.PitchRelease, GameplayInputCommand.PitchRelease);
            AddPitchCommandIfMatches(key, _bindings.PitchFastball, GameplayPitchType.Fastball, GameplayInputCommand.SelectFastball);
            AddPitchCommandIfMatches(key, _bindings.PitchCurveball, GameplayPitchType.Curveball, GameplayInputCommand.SelectCurveball);
            AddPitchCommandIfMatches(key, _bindings.PitchSlider, GameplayPitchType.Slider, GameplayInputCommand.SelectSlider);
            AddPitchCommandIfMatches(key, _bindings.PitchChangeup, GameplayPitchType.Changeup, GameplayInputCommand.SelectChangeup);
            AddPitchCommandIfMatches(key, _bindings.PitchSplitter, GameplayPitchType.Splitter, GameplayInputCommand.SelectSplitter);
            AddPitchCommandIfMatches(key, _bindings.PitchForkball, GameplayPitchType.Forkball, GameplayInputCommand.SelectForkball);
            AddPitchCommandIfMatches(key, _bindings.PitchKnuckleball, GameplayPitchType.Knuckleball, GameplayInputCommand.SelectKnuckleball);
            AddIfMatches(key, _bindings.ThrowHome, GameplayInputCommand.ThrowHome);
            AddIfMatches(key, _bindings.ThrowFirst, GameplayInputCommand.ThrowFirst);
            AddIfMatches(key, _bindings.ThrowSecond, GameplayInputCommand.ThrowSecond);
            AddIfMatches(key, _bindings.ThrowThird, GameplayInputCommand.ThrowThird);
            AddIfMatches(key, _bindings.AdvanceRunners, GameplayInputCommand.AdvanceRunners);
            AddIfMatches(key, _bindings.RetreatRunners, GameplayInputCommand.RetreatRunners);
            AddIfMatches(key, _bindings.SacrificeBunt, GameplayInputCommand.CallSacrificeBunt);
            AddIfMatches(key, _bindings.HitAndRun, GameplayInputCommand.CallHitAndRun);
            AddIfMatches(key, _bindings.Steal, GameplayInputCommand.CallSteal);
            AddIfMatches(key, _bindings.Safe, GameplayInputCommand.CallSafe);
            AddIfMatches(key, _bindings.Bunt, GameplayInputCommand.CallBunt);
            AddIfMatches(key, _bindings.DoubleSteal, GameplayInputCommand.CallDoubleSteal);
            AddIfMatches(key, _bindings.NormalDefense, GameplayInputCommand.CallNormalDefense);
            AddIfMatches(key, _bindings.InfieldIn, GameplayInputCommand.CallInfieldIn);
            AddIfMatches(key, _bindings.DoublePlayDefense, GameplayInputCommand.CallDoublePlay);
            AddIfMatches(key, _bindings.OutfieldIn, GameplayInputCommand.CallOutfieldIn);
            AddIfMatches(key, _bindings.NoDoubles, GameplayInputCommand.CallNoDoubles);
            AddIfMatches(key, _bindings.WheelPlay, GameplayInputCommand.CallWheelPlay);
            AddIfMatches(key, _bindings.IntentionalWalk, GameplayInputCommand.CallIntentionalWalk);
            AddIfMatches(key, _bindings.MoundVisit, GameplayInputCommand.CallMoundVisit);
            AddIfMatches(key, _bindings.ChangePitcher, GameplayInputCommand.ChangePitcher);
            AddIfMatches(key, _bindings.Pause, GameplayInputCommand.TogglePause);
            AddIfMatches(key, _bindings.CameraToggle, GameplayInputCommand.ToggleCamera);
            AddIfMatches(key, _bindings.WatchToggle, GameplayInputCommand.ToggleWatch);
        }

        private static GameplayDirection CombineDirections(GameplayDirection first, GameplayDirection second)
        {
            return new GameplayDirection(first.X + second.X, first.Y + second.Y);
        }

        private void AddPitchCommandIfMatches(
            Keys key,
            IReadOnlyList<Keys> keys,
            GameplayPitchType pitchType,
            GameplayInputCommand command)
        {
            if (!ContainsKey(keys, key))
            {
                return;
            }

            SelectedPitchType = pitchType;
            KeyboardSelectedPitchType = pitchType;
            _pendingKeyboardCommands.Add(command);
        }

        private void AddIfMatches(Keys key, IReadOnlyList<Keys> keys, GameplayInputCommand command)
        {
            if (ContainsKey(keys, key))
            {
                _pendingKeyboardCommands.Add(command);
            }
        }

        private GameplayDirection DirectionFrom(
            IReadOnlyList<Keys> left,
            IReadOnlyList<Keys> right,
            IReadOnlyList<Keys> up,
            IReadOnlyList<Keys> down)
        {
            var x = AxisFrom(left, right);
            var y = AxisFrom(up, down);
            return new GameplayDirection(x, y);
        }

        private int AxisFrom(IReadOnlyList<Keys> negativeKeys, IReadOnlyList<Keys> positiveKeys)
        {
            var negative = AnyDown(negativeKeys);
            var positive = AnyDown(positiveKeys);

            if (negative == positive)
            {
                return 0;
            }

            return positive ? 1 : -1;
        }

        private bool AnyDown(IReadOnlyList<Keys> keys)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                if (_downKeys.Contains(NormalizeKey(keys[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKey(IReadOnlyList<Keys> keys, Keys key)
        {
            var normalizedKey = NormalizeKey(key);
            for (var i = 0; i < keys.Count; i++)
            {
                if (NormalizeKey(keys[i]) == normalizedKey)
                {
                    return true;
                }
            }

            return false;
        }

        private static Keys NormalizeKey(Keys key)
        {
            return key & Keys.KeyCode;
        }
    }
}
