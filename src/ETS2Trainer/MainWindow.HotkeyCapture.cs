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

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ETS2Trainer.Core;

namespace ETS2Trainer;

public partial class MainWindow
{
    private const string DefaultCaptureInstruction = "Değiştirmek için düğmeye tıkla, ardından kullanmak istediğin tek tuşa bas. Esc iptal eder.";
    private bool _capturingMenuHotkey;
    private bool _menuHotkeyCaptureSuspended;
    private Key? _menuHotkeyReleaseKey;
    private string _captureInstruction = DefaultCaptureInstruction;

    public event Action<bool>? MenuHotkeyCaptureStateChanged;

    private void CaptureMenuHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_menuHotkeyReleaseKey is not null)
            return;
        _capturingMenuHotkey = true;
        _captureInstruction = "Şimdi kullanmak istediğin tek tuşa bas. Esc değişikliği iptal eder.";
        SetMenuHotkeyCaptureSuspended(true);
        UpdateMenuHotkeyCapturePresentation();
        CaptureMenuHotkeyButton.Focus();
    }

    private bool TryHandleMenuHotkeyCaptureKeyDown(KeyEventArgs e)
    {
        if (!_capturingMenuHotkey && _menuHotkeyReleaseKey is null)
            return false;

        e.Handled = true;
        if (_menuHotkeyReleaseKey is not null || e.IsRepeat)
            return true;

        var key = CaptureKey(e);
        if (key == Key.Escape)
        {
            _capturingMenuHotkey = false;
            _menuHotkeyReleaseKey = key;
            _captureInstruction = "Tuş değişikliği iptal edildi. Esc tuşunu bırak.";
            UpdateMenuHotkeyCapturePresentation();
            return true;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (Keyboard.Modifiers != ModifierKeys.None || !MenuHotkey.TryFromVirtualKey(virtualKey, out var candidate))
        {
            _captureInstruction = virtualKey switch
            {
                0x7b => "F12 sistem tarafından ayrılmış. Başka bir tuş seç.",
                0x5b or 0x5c => "Windows tuşu atanamaz. Başka bir tuş seç.",
                _ => "Tek bir klavye tuşu seç. Ctrl, Alt ve Shift birleşimleri atanamaz."
            };
            UpdateMenuHotkeyCapturePresentation();
            return true;
        }

        _capturingMenuHotkey = false;
        _menuHotkeyReleaseKey = key;
        var displayName = NativeOverlayPlatform.GetKeyDisplayName(virtualKey);
        if (!string.IsNullOrWhiteSpace(displayName))
            candidate = candidate with { DisplayName = displayName };
        var result = ApplyMenuHotkey(candidate);
        _captureInstruction = result.Success
            ? $"{_menuHotkey.DisplayName} kaydedildi. Devam etmek için tuşu bırak."
            : $"{result.Message} Devam etmek için tuşu bırak.";
        UpdateMenuHotkeyCapturePresentation();
        return true;
    }

    private void MenuHotkeyCaptureKeyUp(object sender, KeyEventArgs e)
    {
        if (!_capturingMenuHotkey && _menuHotkeyReleaseKey is null)
            return;
        e.Handled = true;
        if (_menuHotkeyReleaseKey != CaptureKey(e))
            return;
        _menuHotkeyReleaseKey = null;
        _captureInstruction = DefaultCaptureInstruction;
        SetMenuHotkeyCaptureSuspended(false);
        UpdateMenuHotkeyCapturePresentation();
    }

    private void CancelMenuHotkeyCapture()
    {
        if (!_capturingMenuHotkey && _menuHotkeyReleaseKey is null && !_menuHotkeyCaptureSuspended)
            return;
        _capturingMenuHotkey = false;
        _menuHotkeyReleaseKey = null;
        _captureInstruction = DefaultCaptureInstruction;
        SetMenuHotkeyCaptureSuspended(false);
        UpdateMenuHotkeyCapturePresentation();
    }

    private void SetMenuHotkeyCaptureSuspended(bool suspended)
    {
        if (_menuHotkeyCaptureSuspended == suspended)
            return;
        _menuHotkeyCaptureSuspended = suspended;
        MenuHotkeyCaptureStateChanged?.Invoke(suspended);
    }

    private void UpdateMenuHotkeyCapturePresentation()
    {
        CaptureMenuHotkeyButton.Content = _menuHotkeyReleaseKey is not null
            ? L.Text("Tuşu bırak…") : _capturingMenuHotkey ? L.Text("Bir tuşa bas…") : _menuHotkey.DisplayName;
        CaptureMenuHotkeyButton.IsEnabled = _menuHotkeyReleaseKey is null;
        CaptureMenuHotkeyButton.BorderBrush = (Brush)FindResource(_capturingMenuHotkey ? "AccentBrush" : "LineBrush");
        CaptureInstructionText.Text = L.Message(_captureInstruction);
    }

    private static Key CaptureKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key
    };
}
