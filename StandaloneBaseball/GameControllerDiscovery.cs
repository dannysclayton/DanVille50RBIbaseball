using System;
using System.Runtime.InteropServices;

namespace StandaloneBaseball
{
    internal readonly struct GameControllerReading
    {
        public GameControllerReading(string deviceId, string displayName, int controllerIndex, XInputGamepadState state)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
            ControllerIndex = controllerIndex;
            State = state;
        }

        public string DeviceId { get; }
        public string DisplayName { get; }
        public int ControllerIndex { get; }
        public XInputGamepadState State { get; }
    }

    internal static class GameControllerDiscovery
    {
        public static bool TryReadPreferredOrFirst(
            int preferredXInputIndex,
            string? currentDeviceId,
            out GameControllerReading reading)
        {
            PlayStationControllerGeneration profile = ControllerSettingsStore.Current.Profile;
            if (TryReadDeviceId(currentDeviceId, profile, out reading))
                return true;

            if (preferredXInputIndex >= 0 && TryReadXInput(preferredXInputIndex, out reading))
                return true;

            for (int index = 0; index < XInputController.MaxControllerCount; index++)
            {
                if (index != preferredXInputIndex && TryReadXInput(index, out reading))
                    return true;
            }

            for (int index = 0; index < LegacyJoystickController.DeviceCount; index++)
            {
                if (LegacyJoystickController.TryGetState(index, profile, out XInputGamepadState state))
                {
                    reading = new GameControllerReading(
                        "winmm:" + index,
                        "Windows game controller " + (index + 1),
                        index,
                        state);
                    return true;
                }
            }

            reading = default;
            return false;
        }

        private static bool TryReadDeviceId(
            string? deviceId,
            PlayStationControllerGeneration profile,
            out GameControllerReading reading)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                reading = default;
                return false;
            }

            string[] parts = deviceId.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int index))
            {
                reading = default;
                return false;
            }

            if (string.Equals(parts[0], "xinput", StringComparison.OrdinalIgnoreCase))
                return TryReadXInput(index, out reading);

            if (string.Equals(parts[0], "winmm", StringComparison.OrdinalIgnoreCase) &&
                LegacyJoystickController.TryGetState(index, profile, out XInputGamepadState state))
            {
                reading = new GameControllerReading(
                    "winmm:" + index,
                    "Windows game controller " + (index + 1),
                    index,
                    state);
                return true;
            }

            reading = default;
            return false;
        }

        private static bool TryReadXInput(int index, out GameControllerReading reading)
        {
            if (XInputController.TryGetState(index, out XInputGamepadState state))
            {
                reading = new GameControllerReading(
                    "xinput:" + index,
                    "XInput controller " + (index + 1),
                    index,
                    state);
                return true;
            }

            reading = default;
            return false;
        }
    }

    internal static class LegacyJoystickController
    {
        private const uint MmSystemNoError = 0;
        private const uint JoyReturnAll = 0x000000FF;
        private const uint PovCentered = 0x0000FFFF;

        public static int DeviceCount
        {
            get
            {
                try
                {
                    return (int)Math.Min(16u, JoyGetNumDevs());
                }
                catch (DllNotFoundException)
                {
                    return 0;
                }
                catch (EntryPointNotFoundException)
                {
                    return 0;
                }
            }
        }

        public static bool TryGetState(int controllerIndex, out XInputGamepadState state)
            => TryGetState(controllerIndex, ControllerSettingsStore.Current.Profile, out state);

        public static bool TryGetState(
            int controllerIndex,
            PlayStationControllerGeneration profile,
            out XInputGamepadState state)
        {
            if (controllerIndex < 0 || controllerIndex >= DeviceCount)
            {
                state = default;
                return false;
            }

            var info = new JoyInfoEx
            {
                Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
                Flags = JoyReturnAll
            };

            try
            {
                if (JoyGetPosEx((uint)controllerIndex, ref info) != MmSystemNoError)
                {
                    state = default;
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                state = default;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                state = default;
                return false;
            }

            XInputButtons buttons = MapButtons(info.Buttons, info.Pov, profile);
            byte leftTrigger = TriggerValue(info.Buttons, 0x0040, profile);
            byte rightTrigger = TriggerValue(info.Buttons, 0x0080, profile);
            short x = NormalizeAxis(info.XPosition);
            short y = NormalizeAxis(info.YPosition);
            short rightX = NormalizeAxis(info.RPosition);
            short rightY = NormalizeAxis(info.UPosition);
            int packet = HashCode.Combine(info.Buttons, info.XPosition, info.YPosition, info.Pov);
            state = new XInputGamepadState(
                controllerIndex,
                packet,
                buttons,
                leftTrigger,
                rightTrigger,
                x,
                InvertAxis(y),
                rightX,
                InvertAxis(rightY));
            return true;
        }

        internal static XInputButtons MapButtons(uint buttons, uint pov)
            => MapButtons(buttons, pov, PlayStationControllerGeneration.PlayStation3);

        internal static XInputButtons MapButtons(
            uint buttons,
            uint pov,
            PlayStationControllerGeneration profile)
        {
            XInputButtons mapped = XInputButtons.None;
            bool modernPlayStation = profile == PlayStationControllerGeneration.PlayStation4 ||
                                     profile == PlayStationControllerGeneration.PlayStation5;
            if (modernPlayStation)
            {
                if ((buttons & 0x0001) != 0) mapped |= XInputButtons.X; // Square
                if ((buttons & 0x0002) != 0) mapped |= XInputButtons.A; // Cross
                if ((buttons & 0x0004) != 0) mapped |= XInputButtons.B; // Circle
                if ((buttons & 0x0008) != 0) mapped |= XInputButtons.Y; // Triangle
                if ((buttons & 0x0010) != 0) mapped |= XInputButtons.LeftShoulder;
                if ((buttons & 0x0020) != 0) mapped |= XInputButtons.RightShoulder;
                if ((buttons & 0x0100) != 0) mapped |= XInputButtons.Back; // SHARE / Create
                if ((buttons & 0x0200) != 0) mapped |= XInputButtons.Start; // OPTIONS / Options
                if ((buttons & 0x0400) != 0) mapped |= XInputButtons.LeftThumb;
                if ((buttons & 0x0800) != 0) mapped |= XInputButtons.RightThumb;
            }
            else
            {
                if ((buttons & 0x0001) != 0) mapped |= XInputButtons.A;
                if ((buttons & 0x0002) != 0) mapped |= XInputButtons.B;
                if ((buttons & 0x0004) != 0) mapped |= XInputButtons.X;
                if ((buttons & 0x0008) != 0) mapped |= XInputButtons.Y;
                if ((buttons & 0x0010) != 0) mapped |= XInputButtons.LeftShoulder;
                if ((buttons & 0x0020) != 0) mapped |= XInputButtons.RightShoulder;
                if ((buttons & 0x0040) != 0) mapped |= XInputButtons.Back;
                if ((buttons & 0x0080) != 0) mapped |= XInputButtons.Start;
                if ((buttons & 0x0100) != 0) mapped |= XInputButtons.LeftThumb;
                if ((buttons & 0x0200) != 0) mapped |= XInputButtons.RightThumb;
            }

            if (pov != PovCentered)
            {
                int degrees = (int)(pov / 100u) % 360;
                if (degrees >= 315 || degrees <= 45) mapped |= XInputButtons.DPadUp;
                if (degrees >= 45 && degrees <= 135) mapped |= XInputButtons.DPadRight;
                if (degrees >= 135 && degrees <= 225) mapped |= XInputButtons.DPadDown;
                if (degrees >= 225 && degrees <= 315) mapped |= XInputButtons.DPadLeft;
            }

            return mapped;
        }

        internal static byte TriggerValue(
            uint buttons,
            uint mask,
            PlayStationControllerGeneration profile)
        {
            bool modernPlayStation = profile == PlayStationControllerGeneration.PlayStation4 ||
                                     profile == PlayStationControllerGeneration.PlayStation5;
            return modernPlayStation && (buttons & mask) != 0 ? byte.MaxValue : (byte)0;
        }

        internal static short NormalizeAxis(uint value)
        {
            int centered = (int)Math.Clamp(value, 0u, 65535u) - 32768;
            return (short)Math.Clamp(centered, short.MinValue, short.MaxValue);
        }

        internal static short InvertAxis(short value)
            => value == short.MinValue ? short.MaxValue : (short)-value;

        [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs")]
        private static extern uint JoyGetNumDevs();

        [DllImport("winmm.dll", EntryPoint = "joyGetPosEx")]
        private static extern uint JoyGetPosEx(uint joystickId, ref JoyInfoEx info);

        [StructLayout(LayoutKind.Sequential)]
        private struct JoyInfoEx
        {
            public uint Size;
            public uint Flags;
            public uint XPosition;
            public uint YPosition;
            public uint ZPosition;
            public uint RPosition;
            public uint UPosition;
            public uint VPosition;
            public uint Buttons;
            public uint ButtonNumber;
            public uint Pov;
            public uint Reserved1;
            public uint Reserved2;
        }
    }
}
