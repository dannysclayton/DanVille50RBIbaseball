using System;
using System.Collections.Generic;

namespace StandaloneBaseball
{
    internal sealed class XInputGameplayControllerInput
    {
        private XInputButtons _previousButtons;
        private XInputButtons _currentButtons;
        private byte _previousLeftTrigger;
        private byte _currentLeftTrigger;
        private byte _previousRightTrigger;
        private byte _currentRightTrigger;
        private int _preferredControllerIndex = -1;
        private int _leftStickDeadzone = XInputController.DefaultLeftThumbDeadzone;

        public int? ConnectedControllerIndex { get; private set; }
        public string? ConnectedControllerId { get; private set; }
        public string? ConnectedControllerName { get; private set; }

        public int PreferredControllerIndex
        {
            get => _preferredControllerIndex;
            set => _preferredControllerIndex = value < 0 ? -1 : Math.Min(value, XInputController.MaxControllerCount - 1);
        }

        public int LeftStickDeadzone
        {
            get => _leftStickDeadzone;
            set => _leftStickDeadzone = Math.Clamp(value, 0, short.MaxValue);
        }

        public GameplayDirection LeftStickDirection { get; private set; }

        public GameplayPitchType SelectedPitchType { get; private set; } = GameplayPitchType.Fastball;

        public bool SelectedPitchTypeChanged { get; private set; }

        public bool BatSwingHeld => IsDown(XInputButtons.A);

        public bool BatContactSwingHeld => IsDown(XInputButtons.B);

        public bool BatPowerSwingHeld => IsDown(XInputButtons.X);

        public bool PitchReleaseHeld => IsDown(XInputButtons.A);

        public bool AdvanceRunnersHeld => IsDown(XInputButtons.LeftShoulder);

        public bool RetreatRunnersHeld => IsDown(XInputButtons.RightShoulder);

        public bool Poll(ICollection<GameplayInputCommand> pendingCommands)
        {
            if (pendingCommands == null)
            {
                throw new ArgumentNullException(nameof(pendingCommands));
            }

            SelectedPitchTypeChanged = false;

            if (!TryReadController(out GameControllerReading reading))
            {
                ResetConnectionState();
                return false;
            }

            XInputGamepadState state = reading.State;
            ConnectedControllerIndex = state.ControllerIndex;
            ConnectedControllerId = reading.DeviceId;
            ConnectedControllerName = reading.DisplayName;
            _previousButtons = _currentButtons;
            _currentButtons = state.Buttons;
            _previousLeftTrigger = _currentLeftTrigger;
            _currentLeftTrigger = state.LeftTrigger;
            _previousRightTrigger = _currentRightTrigger;
            _currentRightTrigger = state.RightTrigger;
            LeftStickDirection = DirectionFromLeftStick(state.LeftThumbX, state.LeftThumbY);
            AddCommandsForButtonEdges(pendingCommands);
            return true;
        }

        public void Reset()
        {
            ResetConnectionState();
            SelectedPitchType = GameplayPitchType.Fastball;
            SelectedPitchTypeChanged = false;
        }

        private void ResetConnectionState()
        {
            ConnectedControllerIndex = null;
            ConnectedControllerId = null;
            ConnectedControllerName = null;
            _previousButtons = XInputButtons.None;
            _currentButtons = XInputButtons.None;
            _previousLeftTrigger = 0;
            _currentLeftTrigger = 0;
            _previousRightTrigger = 0;
            _currentRightTrigger = 0;
            LeftStickDirection = default;
        }

        private bool TryReadController(out GameControllerReading reading)
        {
            return GameControllerDiscovery.TryReadPreferredOrFirst(
                _preferredControllerIndex,
                ConnectedControllerId,
                out reading);
        }

        private void AddCommandsForButtonEdges(ICollection<GameplayInputCommand> pendingCommands)
        {
            if (PressedThisPoll(XInputButtons.A))
            {
                pendingCommands.Add(GameplayInputCommand.BatSwing);
                pendingCommands.Add(GameplayInputCommand.PitchRelease);
                pendingCommands.Add(GameplayInputCommand.ThrowHome);
            }

            if (PressedThisPoll(XInputButtons.X))
            {
                pendingCommands.Add(GameplayInputCommand.BatPowerSwing);
                pendingCommands.Add(GameplayInputCommand.ThrowThird);
                AddSelectedPitch(GameplayPitchType.Changeup, GameplayInputCommand.SelectChangeup, pendingCommands);
            }

            if (PressedThisPoll(XInputButtons.Y))
            {
                pendingCommands.Add(GameplayInputCommand.CallSacrificeBunt);
                pendingCommands.Add(GameplayInputCommand.ThrowSecond);
                AddSelectedPitch(GameplayPitchType.Slider, GameplayInputCommand.SelectSlider, pendingCommands);
            }

            if (PressedThisPoll(XInputButtons.B))
            {
                pendingCommands.Add(GameplayInputCommand.BatContactSwing);
                pendingCommands.Add(GameplayInputCommand.ThrowFirst);
                AddSelectedPitch(GameplayPitchType.Curveball, GameplayInputCommand.SelectCurveball, pendingCommands);
            }

            AddPitchCommandIfPressed(
                XInputButtons.DPadUp,
                GameplayPitchType.Fastball,
                GameplayInputCommand.SelectFastball,
                pendingCommands);
            AddPitchCommandIfPressed(
                XInputButtons.DPadLeft,
                GameplayPitchType.Curveball,
                GameplayInputCommand.SelectCurveball,
                pendingCommands);
            AddPitchCommandIfPressed(
                XInputButtons.DPadRight,
                GameplayPitchType.Slider,
                GameplayInputCommand.SelectSlider,
                pendingCommands);
            AddPitchCommandIfPressed(
                XInputButtons.DPadDown,
                GameplayPitchType.Changeup,
                GameplayInputCommand.SelectChangeup,
                pendingCommands);
            AddPitchCommandIfPressed(
                XInputButtons.LeftThumb,
                GameplayPitchType.Fastball,
                GameplayInputCommand.SelectFastball,
                pendingCommands);

            AddIfPressed(XInputButtons.LeftShoulder, GameplayInputCommand.AdvanceRunners, pendingCommands);
            AddIfPressed(XInputButtons.RightShoulder, GameplayInputCommand.RetreatRunners, pendingCommands);
            if (PressedThisPoll(XInputButtons.RightShoulder))
                AddSelectedPitch(GameplayPitchType.Splitter, GameplayInputCommand.SelectSplitter, pendingCommands);
            if (TriggerPressedThisPoll(_currentLeftTrigger, _previousLeftTrigger))
            {
                pendingCommands.Add(GameplayInputCommand.CallSteal);
                AddSelectedPitch(GameplayPitchType.Forkball, GameplayInputCommand.SelectForkball, pendingCommands);
            }
            if (TriggerPressedThisPoll(_currentRightTrigger, _previousRightTrigger))
            {
                pendingCommands.Add(GameplayInputCommand.HoldRunners);
                AddSelectedPitch(GameplayPitchType.Knuckleball, GameplayInputCommand.SelectKnuckleball, pendingCommands);
            }
            AddIfPressed(XInputButtons.Start, GameplayInputCommand.TogglePause, pendingCommands);
            AddIfPressed(XInputButtons.Back, GameplayInputCommand.ToggleWatch, pendingCommands);
            AddIfPressed(XInputButtons.RightThumb, GameplayInputCommand.ToggleCamera, pendingCommands);
        }

        private void AddPitchCommandIfPressed(
            XInputButtons button,
            GameplayPitchType pitchType,
            GameplayInputCommand command,
            ICollection<GameplayInputCommand> pendingCommands)
        {
            if (!PressedThisPoll(button))
            {
                return;
            }

            AddSelectedPitch(pitchType, command, pendingCommands);
        }

        private void AddSelectedPitch(
            GameplayPitchType pitchType,
            GameplayInputCommand command,
            ICollection<GameplayInputCommand> pendingCommands)
        {
            SelectedPitchType = pitchType;
            SelectedPitchTypeChanged = true;
            pendingCommands.Add(command);
        }

        private void AddIfPressed(
            XInputButtons button,
            GameplayInputCommand command,
            ICollection<GameplayInputCommand> pendingCommands)
        {
            if (PressedThisPoll(button))
            {
                pendingCommands.Add(command);
            }
        }

        private GameplayDirection DirectionFromLeftStick(short x, short y)
        {
            var directionX = AxisFromStick(x, false);
            var directionY = AxisFromStick(y, true);
            return new GameplayDirection(directionX, directionY);
        }

        private int AxisFromStick(short value, bool invert)
        {
            if (Math.Abs(value) <= _leftStickDeadzone)
            {
                return 0;
            }

            if (invert)
            {
                return value > 0 ? -1 : 1;
            }

            return value > 0 ? 1 : -1;
        }

        private bool IsDown(XInputButtons button)
        {
            return (_currentButtons & button) == button;
        }

        private bool PressedThisPoll(XInputButtons button)
        {
            return (_currentButtons & button) == button && (_previousButtons & button) != button;
        }

        private static bool TriggerPressedThisPoll(byte current, byte previous)
        {
            return current >= PlayStation3ControllerProfile.TriggerThreshold &&
                   previous < PlayStation3ControllerProfile.TriggerThreshold;
        }
    }
}
