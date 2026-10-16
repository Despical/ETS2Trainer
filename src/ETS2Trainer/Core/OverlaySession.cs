/*
 * ETS2Trainer
 * Copyright (C) 2026 Berke Akçen
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

using System.IO;

namespace ETS2Trainer.Core;

public readonly record struct ScreenBounds(int X, int Y, int Width, int Height);
public sealed record OverlayGame(int ProcessId, nint Window, ScreenBounds Bounds, double DpiScale, bool Minimized = false);
public sealed record OverlayContext(OverlayGame? Game, bool GameForeground, bool MenuForeground);
public sealed record OverlayPlacement(ScreenBounds Physical, double Left, double Top, double Width, double Height);

public interface IOverlayPlatform : IDisposable
{
    OverlayContext Observe();
    bool RegisterHotkey(MenuHotkey key);
    void UnregisterHotkey();
    bool FocusGame(OverlayGame game);
}

public interface IOverlayView
{
    bool Open(OverlayPlacement placement);
    void Reposition(OverlayPlacement placement);
    void Hide();
    void RestoreDesktop();
    void SetHint(string text);
}

// State policy is independent from Windows input and rendering, so lifecycle
// tests can exercise it without registering keys or activating actual windows.
public sealed class OverlaySession(IOverlayPlatform platform, IOverlayView view, Func<DateTime>? clock = null, MenuHotkey? key = null) : IDisposable
{
    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.UtcNow);
    private OverlayGame? _game;
    private OverlayPlacement? _placement;
    private bool _registered, _hiddenForGame, _disposed, _hotkeySuspended;
    private DateTime _nextRegistration;
    private string? _errorHint;
    public bool IsOpen { get; private set; }
    public MenuHotkey Hotkey { get; private set; } = key ?? MenuHotkey.Default;
    public const string DefaultHint = "Oyunda Insert · menüyü aç / kapat";
    public string CurrentHint => $"Oyunda {Hotkey.DisplayName} · menüyü aç / kapat";

    public void Tick()
    {
        if (_disposed) return;
        var context = platform.Observe();
        if (_game is { } previous && (context.Game is null || context.Game.ProcessId != previous.ProcessId || context.Game.Window != previous.Window))
        {
            if (IsOpen || _hiddenForGame) view.RestoreDesktop();
            IsOpen = _hiddenForGame = false;
            _placement = null;
        }
        _game = context.Game;
        if (IsOpen)
        {
            if (context.Game is { Minimized: true } && context.MenuForeground)
            {
                view.RestoreDesktop(); IsOpen = _hiddenForGame = false;
                Report("ETS2 küçüldü · oyun üstü menü için kenarlıksız / pencereli ekran modunu kullanın.");
            }
            else if (context.Game is null || context.Game.Minimized || (!context.GameForeground && !context.MenuForeground))
                Hide();
            else if (TryPlace(context.Game, out var placement) && placement != _placement)
            {
                view.Reposition(placement!);
                _placement = placement;
            }
        }

        var shouldRegister = !_hotkeySuspended && context.Game is { Minimized: false } && (context.GameForeground || IsOpen && context.MenuForeground);
        if (!shouldRegister && _registered) { platform.UnregisterHotkey(); _registered = false; }
        if (shouldRegister && !_registered && _clock() >= _nextRegistration)
        {
            _registered = platform.RegisterHotkey(Hotkey);
            _nextRegistration = _registered ? DateTime.MinValue : _clock().AddSeconds(2);
        }
        view.SetHint(shouldRegister && !_registered ? $"{Hotkey.DisplayName} başka bir uygulamada kullanılıyor · farklı bir tuş seçin" : _errorHint ?? CurrentHint);
    }

    public HotkeyUpdateResult ChangeHotkey(MenuHotkey requested, Action<MenuHotkey>? save = null)
    {
        if (_disposed) return new(false, "Menü bağlantısı kapatıldı.");
        if (!MenuHotkey.TryFind(requested.Id, out var candidate) || candidate.VirtualKey != requested.VirtualKey)
            return new(false, "Listeden desteklenen bir menü tuşu seçin.");
        candidate = candidate with { DisplayName = MenuHotkey.CleanDisplayName(requested.DisplayName, candidate.DisplayName) };
        if (candidate.Id == Hotkey.Id) return new(true, $"Menü tuşu zaten {Hotkey.DisplayName}.");
        var previous = Hotkey;
        var wasRegistered = _registered;
        if (_registered) { platform.UnregisterHotkey(); _registered = false; }
        if (!platform.RegisterHotkey(candidate))
        {
            _registered = wasRegistered && platform.RegisterHotkey(previous);
            _nextRegistration = DateTime.MinValue;
            return new(false, $"{candidate.DisplayName} başka bir uygulamada kullanılıyor. Menü tuşu {previous.DisplayName} olarak kaldı.");
        }
        try { save?.Invoke(candidate); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            platform.UnregisterHotkey();
            _registered = wasRegistered && platform.RegisterHotkey(previous);
            _nextRegistration = DateTime.MinValue;
            return new(false, $"Ayar kaydedilemedi. Menü tuşu {previous.DisplayName} olarak kaldı.");
        }
        Hotkey = candidate;
        _registered = true;
        if (!wasRegistered) { platform.UnregisterHotkey(); _registered = false; }
        _errorHint = null; _nextRegistration = DateTime.MinValue;
        view.SetHint(CurrentHint);
        return new(true, $"Menü tuşu {Hotkey.DisplayName} olarak kaydedildi.");
    }

    public void SuspendHotkey(bool suspended)
    {
        if (_disposed) return;
        _hotkeySuspended = suspended;
        if (suspended && _registered) { platform.UnregisterHotkey(); _registered = false; }
        if (!suspended) _nextRegistration = DateTime.MinValue;
    }

    public void Toggle()
    {
        if (_disposed || _hotkeySuspended) return;
        var context = platform.Observe();
        if (IsOpen)
        {
            Dismiss(context);
            return;
        }
        // A queued WM_HOTKEY must never steal focus after the user alt-tabs.
        if (!context.GameForeground || context.Game is not { Minimized: false } game) return;
        _errorHint = null;
        if (!TryPlace(game, out var placement))
        {
            Report("Menü için oyun penceresini büyütün (en az 784 × 544 ölçekli piksel).");
            return;
        }
        if (!view.Open(placement!))
        {
            view.RestoreDesktop();
            Report("Menü öne getirilemedi. ETS2'de kenarlıksız veya pencereli ekran modunu kullanın.");
            return;
        }
        _game = game; _placement = placement;
        IsOpen = _hiddenForGame = true;
    }

    public void Dismiss() { if (!_disposed) Dismiss(platform.Observe()); }

    private void Dismiss(OverlayContext context)
    {
        if (context.Game is not { Minimized: false } game)
        {
            if (IsOpen) { view.RestoreDesktop(); IsOpen = _hiddenForGame = false; }
            Report("Menüyü oyun üzerinde kullanmak için ETS2'yi açın.");
            return;
        }
        var mayFocus = context.GameForeground || context.MenuForeground;
        Hide();
        _game = game;
        if (mayFocus) platform.FocusGame(game);
    }

    private void Hide()
    {
        view.Hide(); IsOpen = false; _hiddenForGame = true; _placement = null;
    }

    private void Report(string text) { _errorHint = text; view.SetHint(text); }

    public static bool TryPlace(OverlayGame game, out OverlayPlacement? placement)
    {
        placement = null;
        var scale = game.DpiScale;
        if (!double.IsFinite(scale) || scale is < .5 or > 5) return false;
        var bounds = game.Bounds;
        var width = Math.Min(980d, bounds.Width / scale - 24);
        var height = Math.Min(720d, bounds.Height / scale - 24);
        if (width < 760 || height < 520) return false;
        var physicalWidth = (int)Math.Round(width * scale);
        var physicalHeight = (int)Math.Round(height * scale);
        var x = bounds.X + (bounds.Width - physicalWidth) / 2;
        var y = bounds.Y + (bounds.Height - physicalHeight) / 2;
        placement = new(new(x, y, physicalWidth, physicalHeight), x / scale, y / scale, width, height);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered) platform.UnregisterHotkey();
        _registered = false;
        platform.Dispose();
    }
}
