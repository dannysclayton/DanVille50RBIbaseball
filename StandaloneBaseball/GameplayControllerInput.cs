using System;
using System.Collections.Generic;

namespace StandaloneBaseball
{
    internal sealed class XInputGameplayControllerInput
    {
        private XInputButtons _previousButtons;
        private XInputButtons _currentButtons;
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

        public bool BatContactSwingHeld => IsDown(XInputButtons.X);

        public bool BatPowerSwingHeld => IsDown(XInputButtons.Y);

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
                pendingCommands.Add(GameplayInputCommand.BatContactSwing);
                pendingCommands.Add(GameplayInputCommand.ThrowThird);
            }

            if (PressedThisPoll(XInputButtons.Y))
            {
                pendingCommands.Add(GameplayInputCommand.BatPowerSwing);
                pendingCommands.Add(GameplayInputCommand.ThrowSecond);
            }

            if (PressedThisPoll(XInputButtons.B))
            {
                pendingCommands.Add(GameplayInputCommand.ThrowFirst);
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
                GameplayPitchType.Splitter,
                GameplayInputCommand.SelectSplitter,
                pendingCommands);

            AddIfPressed(XInputButtons.LeftShoulder, GameplayInputCommand.AdvanceRunners, pendingCommands);
            AddIfPressed(XInputButtons.RightShoulder, GameplayInputCommand.RetreatRunners, pendingCommands);
            AddIfPressed(XInputButtons.Start, GameplayInputCommand.TogglePause, pendingCommands);
            AddIfPressed(XInputButtons.Back, GameplayInputCommand.ToggleCamera, pendingCommands);
            AddIfPressed(XInputButtons.RightThumb, GameplayInputCommand.ToggleWatch, pendingCommands);
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
    }
}
