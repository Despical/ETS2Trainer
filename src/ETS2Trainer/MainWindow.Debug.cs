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
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ETS2Trainer.Core;
using Microsoft.Win32;

namespace ETS2Trainer;

public partial class MainWindow
{
    private readonly GameDebugSettingsService _debugSettings;
    private string _gameDirectory;
    private bool _debugReady, _loadingDebugProfiles, _capturingCameraKey;
    private Key? _cameraReleaseKey;
    private string? _cameraToken;
    private string _cameraKeyDisplay = "0";
    private string _cameraInstruction = DefaultCaptureInstruction;
    private string _debugStatus = "";

    private void InitializeDebugSettings() => ReloadDebugProfiles();

    private void ReloadDebugProfiles()
    {
        CancelCameraKeyCapture();
        DebugDirectoryText.Text = _gameDirectory;
        DebugDirectoryText.ToolTip = _gameDirectory;
        var selected = (DebugProfileCombo.SelectedItem as GameDebugProfile)?.ControlsPath;
        try
        {
            _loadingDebugProfiles = true;
            var profiles = _debugSettings.Profiles(_gameDirectory);
            DebugProfileCombo.ItemsSource = profiles;
            // Require an explicit choice when several profiles exist.
            DebugProfileCombo.SelectedItem = profiles.FirstOrDefault(p => p.ControlsPath == selected)
                ?? (profiles.Count == 1 ? profiles[0] : null);
        }
        catch (Exception error) { ShowDebugError(error); return; }
        finally { _loadingDebugProfiles = false; }
        ReadDebugSettings();
    }

    private void ReadDebugSettings()
    {
        CancelCameraKeyCapture();
        try
        {
            var profile = DebugProfileCombo.SelectedItem as GameDebugProfile;
            var settings = _debugSettings.Read(_gameDirectory, profile);
            DeveloperModeCheck.IsChecked = settings.Developer;
            DeveloperConsoleCheck.IsChecked = settings.Console;
            FreeCameraCheck.IsChecked = settings.FreeCamera;
            _cameraToken = null; // Preserve an existing custom expression unless a new key is chosen.
            var key = Regex.Match(settings.CameraExpression, @"^keyboard\.(?<key>\w+)\?0$");
            var token = key.Success ? key.Groups["key"].Value : null;
            _cameraKeyDisplay = settings.CameraExpression.Trim() == "0" ? "0"
                : token is null ? "Özel atama" : CameraTokenDisplay(token);
            _debugReady = true;
            _debugStatus = profile is null ? "Kamera için profil seç. Debug ve konsol ayarları tüm profillerde geçerlidir."
                : "Ayarlar okundu. Değişiklikler Kaydet düğmesiyle uygulanır.";
            DebugStatusText.Foreground = (Brush)FindResource("MutedBrush");
        }
        catch (Exception error) { ShowDebugError(error); }
        UpdateDebugAvailability();
    }

    private void DebugProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingDebugProfiles && _debugSettings is not null) ReadDebugSettings();
    }

    private void ChooseGameDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = L.Text("ETS2 kullanıcı klasörünü seç"), Multiselect = false };
        if (Directory.Exists(_gameDirectory)) dialog.InitialDirectory = _gameDirectory;
        if (dialog.ShowDialog(this) != true) return;
        _gameDirectory = dialog.FolderName;
        ReloadDebugProfiles();
    }

    private void DebugOption_Click(object sender, RoutedEventArgs e)
    {
        CancelCameraKeyCapture();
        if (ReferenceEquals(sender, DeveloperModeCheck) && DeveloperModeCheck.IsChecked != true)
        {
            DeveloperConsoleCheck.IsChecked = false;
            FreeCameraCheck.IsChecked = false;
        }
        else if (DeveloperConsoleCheck.IsChecked == true || FreeCameraCheck.IsChecked == true)
            DeveloperModeCheck.IsChecked = true;
        _debugStatus = "Kaydedilmemiş değişiklikler var. Kaydettikten sonra ETS2'yi yeniden başlat.";
        UpdateDebugAvailability();
    }

    private void ReloadDebug_Click(object sender, RoutedEventArgs e) => ReloadDebugProfiles();

    private void SaveDebug_Click(object sender, RoutedEventArgs e)
    {
        CancelCameraKeyCapture();
        try
        {
            if (FreeCameraCheck.IsChecked == true && _cameraToken is not null
                && Enumerable.Range(0, 256).Any(key => GameCameraKey.FromVirtualKey((uint)key) == _cameraToken && key == _menuHotkey.VirtualKey))
                throw new InvalidOperationException("Bu tuş trainer menüsünde kullanılıyor. Farklı bir kamera tuşu seç.");
            var backups = _debugSettings.Save(_gameDirectory, DebugProfileCombo.SelectedItem as GameDebugProfile,
                DeveloperModeCheck.IsChecked == true, DeveloperConsoleCheck.IsChecked == true,
                FreeCameraCheck.IsChecked == true, _cameraToken);
            ReadDebugSettings();
            if (!_debugReady) return;
            _debugStatus = backups.Count == 0 ? "Ayarlar zaten güncel. ETS2'yi başlatabilirsin."
                : "Debug ayarları kaydedildi. ETS2'yi başlat. Değişen dosyaların yanında .ets2trainer yedekleri oluşturuldu.";
            SetOutcome(_debugStatus, true);
            AddActivity(_debugStatus);
        }
        catch (Exception error)
        {
            _debugStatus = $"Debug ayarları kaydedilemedi: {error.Message}";
            DebugStatusText.Foreground = (Brush)FindResource("AccentBrush");
            SetOutcome(_debugStatus, false);
            AddActivity(_debugStatus);
        }
        UpdateDebugAvailability();
    }

    private void ShowDebugError(Exception error)
    {
        _debugReady = false;
        _debugStatus = $"Debug ayarları okunamadı: {error.Message}";
        DebugStatusText.Foreground = (Brush)FindResource("AccentBrush");
        UpdateDebugAvailability();
    }

    private void UpdateDebugAvailability()
    {
        if (_debugSettings is null) return;
        var running = _debugSettings.GameRunning;
        DebugOptionsCard.IsEnabled = _debugReady;
        var profile = DebugProfileCombo.SelectedItem is GameDebugProfile;
        FreeCameraCheck.IsEnabled = profile;
        CaptureCameraKeyButton.IsEnabled = profile && FreeCameraCheck.IsChecked == true && _cameraReleaseKey is null;
        ResetCameraKeyButton.IsEnabled = profile && FreeCameraCheck.IsChecked == true;
        SaveDebugButton.IsEnabled = _debugReady && !running && !_capturingCameraKey && _cameraReleaseKey is null;
        DebugRestartText.Text = L.Text(running
            ? "ETS2 açık. Ayarları kaydetmeden önce oyunu tamamen kapat; kaydettikten sonra yeniden başlat."
            : "Ayarları kaydet, ardından ETS2'yi başlat. Değişiklikler oyun açılırken yüklenir.");
        SaveDebugButton.ToolTip = DebugRestartText.Text;
        DebugStatusText.Text = L.Message(_debugStatus);
        CameraKeyInstructionText.Text = L.Message(_cameraInstruction);
        CaptureCameraKeyButton.Content = _cameraReleaseKey is not null ? L.Text("Tuşu bırak…")
            : _capturingCameraKey ? L.Text("Bir tuşa bas…") : L.Text(_cameraKeyDisplay);
        CaptureCameraKeyButton.BorderBrush = (Brush)FindResource(_capturingCameraKey ? "AccentBrush" : "LineBrush");
    }

    private void CaptureCameraKey_Click(object sender, RoutedEventArgs e)
    {
        CancelMenuHotkeyCapture();
        _capturingCameraKey = true;
        _cameraInstruction = "Şimdi kullanmak istediğin tek tuşa bas. Esc değişikliği iptal eder.";
        SetMenuHotkeyCaptureSuspended(true);
        UpdateDebugAvailability();
        CaptureCameraKeyButton.Focus();
    }

    private bool TryHandleCameraKeyDown(KeyEventArgs e)
    {
        if (!_capturingCameraKey && _cameraReleaseKey is null) return false;
        e.Handled = true;
        if (_cameraReleaseKey is not null || e.IsRepeat) return true;
        var key = CaptureKey(e);
        if (key == Key.Escape)
        {
            _capturingCameraKey = false;
            _cameraReleaseKey = key;
            _cameraInstruction = "Tuş değişikliği iptal edildi. Esc tuşunu bırak.";
        }
        else
        {
            var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            var token = GameCameraKey.FromVirtualKey(virtualKey);
            if (Keyboard.Modifiers != ModifierKeys.None || token is null)
                _cameraInstruction = "Tek bir harf, rakam, işlev veya gezinme tuşu seç. Ctrl, Alt ve Shift birleşimleri desteklenmiyor.";
            else if (virtualKey == _menuHotkey.VirtualKey)
                _cameraInstruction = "Bu tuş trainer menüsünde kullanılıyor. Farklı bir kamera tuşu seç.";
            else
            {
                try
                {
                    var conflict = DebugProfileCombo.SelectedItem is GameDebugProfile profile ? _debugSettings.FindKeyConflict(profile, token) : null;
                    if (conflict is not null)
                        _cameraInstruction = $"Bu tuş başka bir oyun kontrolünde kullanılıyor: {conflict}. Farklı bir tuş seç.";
                    else
                    {
                        _cameraToken = token;
                        _cameraKeyDisplay = NativeOverlayPlatform.GetKeyDisplayName(virtualKey);
                        if (string.IsNullOrWhiteSpace(_cameraKeyDisplay)) _cameraKeyDisplay = CameraTokenDisplay(token);
                        _capturingCameraKey = false;
                        _cameraReleaseKey = key;
                        _cameraInstruction = "Tuş seçildi. Tuşu bırak, ardından debug ayarlarını kaydet.";
                        _debugStatus = "Kaydedilmemiş değişiklikler var. Kaydettikten sonra ETS2'yi yeniden başlat.";
                    }
                }
                catch (Exception error) { _cameraInstruction = error.Message; }
            }
        }
        UpdateDebugAvailability();
        return true;
    }

    private void CameraKeyUp(object sender, KeyEventArgs e)
    {
        if (!_capturingCameraKey && _cameraReleaseKey is null) return;
        e.Handled = true;
        if (_cameraReleaseKey != CaptureKey(e)) return;
        CancelCameraKeyCapture();
    }

    private void CancelCameraKeyCapture()
    {
        if (!_capturingCameraKey && _cameraReleaseKey is null) return;
        _capturingCameraKey = false;
        _cameraReleaseKey = null;
        _cameraInstruction = DefaultCaptureInstruction;
        SetMenuHotkeyCaptureSuspended(false);
        UpdateDebugAvailability();
    }

    private void ResetCameraKey_Click(object sender, RoutedEventArgs e)
    {
        CancelCameraKeyCapture();
        _cameraToken = "key0";
        _cameraKeyDisplay = "0";
        _debugStatus = "Kaydedilmemiş değişiklikler var. Kaydettikten sonra ETS2'yi yeniden başlat.";
        UpdateDebugAvailability();
    }

    private static string CameraTokenDisplay(string token)
    {
        var virtualKey = Enumerable.Range(1, 255).FirstOrDefault(key => GameCameraKey.FromVirtualKey((uint)key) == token);
        var display = virtualKey == 0 ? "" : NativeOverlayPlatform.GetKeyDisplayName((uint)virtualKey);
        return string.IsNullOrWhiteSpace(display) ? token.ToUpperInvariant() : display;
    }
}
