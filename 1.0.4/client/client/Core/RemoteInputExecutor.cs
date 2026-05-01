using System.Runtime.InteropServices;

namespace client.Core;

internal static class RemoteInputExecutor
{
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventWheel = 0x0800;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfExtendedKey = 0x0001;
    private const uint KeyeventfKeyUp = 0x0002;

    public static void Apply(RemoteInputCommand command)
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        switch (command.EventName)
        {
            case "mouse_move":
                MoveMouse(screen.Bounds, command.XRatio, command.YRatio);
                break;
            case "mouse_down":
                MoveMouse(screen.Bounds, command.XRatio, command.YRatio);
                mouse_event(MapButtonDown(command.Button), 0, 0, 0, UIntPtr.Zero);
                break;
            case "mouse_up":
                MoveMouse(screen.Bounds, command.XRatio, command.YRatio);
                mouse_event(MapButtonUp(command.Button), 0, 0, 0, UIntPtr.Zero);
                break;
            case "mouse_wheel":
                mouse_event(MouseEventWheel, 0, 0, unchecked((uint)(command.Delta ?? 0)), UIntPtr.Zero);
                break;
            case "key_down":
                if (command.KeyCode.HasValue)
                {
                    SendKeyboardInput(command.KeyCode.Value, false);
                }
                break;
            case "key_up":
                if (command.KeyCode.HasValue)
                {
                    SendKeyboardInput(command.KeyCode.Value, true);
                }
                break;
        }
    }

    private static void MoveMouse(Rectangle bounds, double? xRatio, double? yRatio)
    {
        if (!xRatio.HasValue || !yRatio.HasValue)
        {
            return;
        }

        var x = bounds.Left + (int)(Math.Clamp(xRatio.Value, 0, 1) * bounds.Width);
        var y = bounds.Top + (int)(Math.Clamp(yRatio.Value, 0, 1) * bounds.Height);
        SetCursorPos(x, y);
    }

    private static uint MapButtonDown(string? button)
    {
        return string.Equals(button, "right", StringComparison.OrdinalIgnoreCase)
            ? MouseEventRightDown
            : MouseEventLeftDown;
    }

    private static uint MapButtonUp(string? button)
    {
        return string.Equals(button, "right", StringComparison.OrdinalIgnoreCase)
            ? MouseEventRightUp
            : MouseEventLeftUp;
    }

    private static void SendKeyboardInput(int keyCode, bool keyUp)
    {
        var virtualKey = (ushort)keyCode;
        var flags = keyUp ? KeyeventfKeyUp : 0U;
        if (IsExtendedKey(virtualKey))
        {
            flags |= KeyeventfExtendedKey;
        }

        var inputs = new INPUT[]
        {
            new()
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == 0)
        {
            keybd_event((byte)virtualKey, 0, flags, UIntPtr.Zero);
        }
    }

    private static bool IsExtendedKey(ushort virtualKey)
    {
        return virtualKey is
            0x21 or
            0x22 or
            0x23 or
            0x24 or
            0x25 or
            0x26 or
            0x27 or
            0x28 or
            0x2D or
            0x2E or
            0x5B or
            0x5C or
            0x5D or
            0xA3 or
            0xA5;
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
