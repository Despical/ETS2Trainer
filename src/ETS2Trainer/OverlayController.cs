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

using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ETS2Trainer.Core;

namespace ETS2Trainer;

internal sealed class OverlayController : IDisposable, IOverlayView
{
    private readonly MainWindow _window;
    private readonly HwndSource _source;
    private readonly OverlaySession _session;
    private readonly DispatcherTimer _timer;
    private readonly nint _handle;
    private bool _disposed;
    private string? _lastHint;
    private readonly MenuHotkeyStore _settings = new();

    public OverlayController(MainWindow window)
    {
        _window = window;
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WindowMessage);
        var saved = _settings.Load();
        _session = new(new NativeOverlayPlatform(_handle), this, key: saved.Key);
        _window.SetMenuHotkey(saved.Key);
        _window.MenuHotkeyChangeRequested += OnHotkeyChange;
        _window.MenuHotkeyCaptureStateChanged += OnHotkeyCapture;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
        _window.OverlayDismissRequested += OnDismiss;
        _window.Closed += OnClosed;
        _timer.Start();
        _session.Tick();
        if (saved.Message.Length > 0) { _window.SetOverlayHint(saved.Message); _window.RecordOverlayActivity(saved.Message); }
    }

    private HotkeyUpdateResult OnHotkeyChange(MenuHotkey key) => _session.ChangeHotkey(key, _settings.Save);
    private void OnHotkeyCapture(bool active) => _session.SuspendHotkey(active);

    private void OnTick(object? sender, EventArgs args) => _session.Tick();
    private void OnDismiss(object? sender, EventArgs args) => _session.Dismiss();
    private void OnClosed(object? sender, EventArgs args) => Dispose();
    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == NativeOverlayPlatform.HotkeyId)
        {
            handled = true;
            _session.Toggle();
        }
        return 0;
    }

    public bool Open(OverlayPlacement placement)
    {
        _window.EnterOverlay(new Rect(placement.Left, placement.Top, placement.Width, placement.Height));
        _window.Show();
        NativeOverlayPlatform.PositionMenu(_handle, placement.Physical);
        if (_window.Activate())
        {
            _window.RecordOverlayActivity($"Oyun üstü menü açıldı · {_session.Hotkey.DisplayName} / Esc ile oyuna dön.");
            return true;
        }
        _window.Hide();
        _window.ExitOverlay();
        return false;
    }

    public void Reposition(OverlayPlacement placement)
    {
        _window.EnterOverlay(new Rect(placement.Left, placement.Top, placement.Width, placement.Height));
        NativeOverlayPlatform.PositionMenu(_handle, placement.Physical);
    }

    public void Hide()
    {
        if (_window.IsOverlayMode) _window.RecordOverlayActivity("Oyun üstü menü gizlendi.");
        _window.Hide();
        _window.ExitOverlay();
    }

    public void RestoreDesktop()
    {
        _window.ExitOverlay();
        // Don't bring a restored desktop window in front of another app.
        var activate = _window.ShowActivated;
        _window.ShowActivated = false;
        _window.Show();
        _window.ShowActivated = activate;
    }

    public void SetHint(string text)
    {
        if (text == _lastHint) return;
        _lastHint = text;
        _window.SetOverlayHint(text);
        if (text != _session.CurrentHint) _window.RecordOverlayActivity(text);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _window.OverlayDismissRequested -= OnDismiss;
        _window.MenuHotkeyChangeRequested -= OnHotkeyChange;
        _window.MenuHotkeyCaptureStateChanged -= OnHotkeyCapture;
        _window.Closed -= OnClosed;
        if (!_source.IsDisposed) _source.RemoveHook(WindowMessage);
        _session.Dispose();
        _window.ExitOverlay();
    }
}
