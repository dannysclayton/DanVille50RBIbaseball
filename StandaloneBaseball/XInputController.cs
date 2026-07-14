using System;
using System.Runtime.InteropServices;

namespace StandaloneBaseball
{
    [Flags]
    public enum XInputButtons
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
    }

    public readonly struct XInputGamepadState
    {
        public XInputGamepadState(
            int controllerIndex,
            int packetNumber,
            XInputButtons buttons,
            byte leftTrigger,
            byte rightTrigger,
            short leftThumbX,
            short leftThumbY,
            short rightThumbX,
            short rightThumbY)
        {
            ControllerIndex = controllerIndex;
            PacketNumber = packetNumber;
            Buttons = buttons;
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            LeftThumbX = leftThumbX;
            LeftThumbY = leftThumbY;
            RightThumbX = rightThumbX;
            RightThumbY = rightThumbY;
        }

        public int ControllerIndex { get; }

        public int PacketNumber { get; }

        public XInputButtons Buttons { get; }

        public byte LeftTrigger { get; }

        public byte RightTrigger { get; }

        public short LeftThumbX { get; }

        public short LeftThumbY { get; }

        public short RightThumbX { get; }

        public short RightThumbY { get; }
    }

    public static class XInputController
    {
        public const int MaxControllerCount = 4;
        public const int DefaultLeftThumbDeadzone = 7849;

        private const int ErrorSuccess = 0;
        private const int ErrorDeviceNotConnected = 1167;

        public static bool TryGetState(int controllerIndex, out XInputGamepadState state)
        {
            if (controllerIndex < 0 || controllerIndex >= MaxControllerCount)
            {
                state = default;
                return false;
            }

            var result = GetState(controllerIndex, out var nativeState);
            if (result != ErrorSuccess)
            {
                state = default;
                return false;
            }

            state = new XInputGamepadState(
                controllerIndex,
                unchecked((int)nativeState.PacketNumber),
                (XInputButtons)nativeState.Gamepad.Buttons,
                nativeState.Gamepad.LeftTrigger,
                nativeState.Gamepad.RightTrigger,
                nativeState.Gamepad.LeftThumbX,
                nativeState.Gamepad.LeftThumbY,
                nativeState.Gamepad.RightThumbX,
                nativeState.Gamepad.RightThumbY);
            return true;
        }

        public static bool TryFindConnectedController(out int controllerIndex)
        {
            for (var i = 0; i < MaxControllerCount; i++)
            {
                if (TryGetState(i, out _))
                {
                    controllerIndex = i;
                    return true;
                }
            }

            controllerIndex = -1;
            return false;
        }

        private static int GetState(int controllerIndex, out NativeXInputState state)
        {
            try
            {
                return XInputGetState14(controllerIndex, out state);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            try
            {
                return XInputGetState910(controllerIndex, out state);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            state = default;
            return ErrorDeviceNotConnected;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int dwUserIndex, out NativeXInputState pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState910(int dwUserIndex, out NativeXInputState pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeXInputState
        {
            public uint PacketNumber;
            public NativeXInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeXInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short LeftThumbX;
            public short LeftThumbY;
            public short RightThumbX;
            public short RightThumbY;
        }
    }
}
