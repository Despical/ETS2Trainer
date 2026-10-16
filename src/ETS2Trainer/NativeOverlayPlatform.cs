/*
 * ETS2Trainer
 * Copyright (C) 2026 Despical
 * SPDX-License-Identifier: GPL-3.0-or-later
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ETS2Trainer.Core;

namespace ETS2Trainer;

internal sealed class NativeOverlayPlatform(nint menuWindow) : IOverlayPlatform
{
    internal const int HotkeyId = 0x4554;
    private Process? _process;
    private DateTime _nextScan;
    private readonly uint _menuPid = (uint)Environment.ProcessId;

    public OverlayContext Observe()
    {
        try
        {
            if (_process is not null && _process.HasExited) { _process.Dispose(); _process = null; }
            if (_process is null && DateTime.UtcNow >= _nextScan)
            {
                _nextScan = DateTime.UtcNow.AddSeconds(1);
                var candidates = Process.GetProcessesByName("eurotrucks2");
                if (candidates.Length == 1) _process = candidates[0];
                else foreach (var candidate in candidates) candidate.Dispose();
            }
            var foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out var foregroundPid);
            var menuForeground = foregroundPid == _menuPid;
            if (_process is null) return new(null, false, menuForeground);
            _process.Refresh();
            var window = _process.MainWindowHandle;
            if (window == 0 || !IsWindow(window)) return new(null, false, menuForeground);
            GetWindowThreadProcessId(window, out var windowPid);
            if (windowPid != _process.Id) return new(null, false, menuForeground);
            var minimized = IsIconic(window) || !IsWindowVisible(window);
            if (!GetClientRect(window, out var client)) return new(null, false, menuForeground);
            var origin = new Point();
            if (!ClientToScreen(window, ref origin)) return new(null, false, menuForeground);
            var dpi = GetDpiForWindow(window);
            var game = new OverlayGame(_process.Id, window,
                new(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top), dpi == 0 ? 1 : dpi / 96d, minimized);
            return new(game, foregroundPid == windowPid, menuForeground);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _process?.Dispose(); _process = null;
            return new(null, false, false);
        }
    }

    public bool RegisterHotkey(MenuHotkey key) => RegisterHotKey(menuWindow, HotkeyId, 0x4000, key.VirtualKey);
    public void UnregisterHotkey() => UnregisterHotKey(menuWindow, HotkeyId);
    public bool FocusGame(OverlayGame game)
    {
        if (!IsWindow(game.Window)) return false;
        GetWindowThreadProcessId(game.Window, out var pid);
        return pid == game.ProcessId && SetForegroundWindow(game.Window);
    }

    public static void PositionMenu(nint window, ScreenBounds bounds) =>
        SetWindowPos(window, (nint)(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0010 | 0x0040);

    public static string GetKeyDisplayName(uint virtualKey)
    {
        var scan = MapVirtualKey(virtualKey, 4);
        var flags = (int)((scan & 0xff) << 16) | ((scan & 0xff00) == 0xe000 ? 1 << 24 : 0);
        var text = new StringBuilder(64);
        return GetKeyNameText(flags, text, text.Capacity) > 0 ? text.ToString()
            : MenuHotkey.TryFromVirtualKey(virtualKey, out var key) ? key.DisplayName : "Tuş";
    }

    public void Dispose() { _process?.Dispose(); _process = null; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] private static extern uint MapVirtualKey(uint key, uint mode);
    [DllImport("user32.dll", EntryPoint = "GetKeyNameTextW", CharSet = CharSet.Unicode)] private static extern int GetKeyNameText(int key, StringBuilder text, int size);
}
