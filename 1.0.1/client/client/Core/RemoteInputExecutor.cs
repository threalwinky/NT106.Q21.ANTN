using System.Runtime.InteropServices;

namespace client.Core;

internal static class RemoteInputExecutor
{
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventWheel = 0x0800;
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
                    keybd_event((byte)command.KeyCode.Value, 0, 0, UIntPtr.Zero);
                }
                break;
            case "key_up":
                if (command.KeyCode.HasValue)
                {
                    keybd_event((byte)command.KeyCode.Value, 0, KeyeventfKeyUp, UIntPtr.Zero);
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

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}

