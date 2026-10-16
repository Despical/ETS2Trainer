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

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using ETS2Trainer.Core;

namespace ETS2Trainer;

public partial class MainWindow
{
    private readonly DesktopPreferencesStore? _desktopStore;
    private readonly Action<string> _openSettingsDirectory;
    private readonly IGameAutoStartService? _autoStartService;
    private DesktopPreferences _desktopPreferences = new();
    internal bool AutoCloseWithGame => _desktopPreferences.AutoCloseWithGame;
    internal event EventHandler? AutoClosePreferenceChanged;

    private void InitializeDesktopPreferences()
    {
        SettingsDirectoryText.Text = DesktopPreferencesStore.DirectoryPath;
        SettingsDirectoryText.ToolTip = SettingsDirectoryText.Text;
        AutoStartCheck.IsEnabled = false;
        AutoCloseCheck.IsEnabled = _desktopStore is not null;
        if (_desktopStore is null) return;
        try
        {
            _desktopPreferences = _desktopStore.Load();
            Topmost = _desktopPreferences.AlwaysOnTop;
            AlwaysOnTopCheck.IsChecked = _desktopPreferences.AlwaysOnTop;
            AutoStartCheck.IsChecked = _desktopPreferences.AutoStartWithGame;
            AutoCloseCheck.IsChecked = _desktopPreferences.AutoCloseWithGame;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            AddActivity("Arayüz tercihleri okunamadı; varsayılanlar kullanılıyor.");
        }
    }

    private void SaveAlwaysOnTop()
    {
        Topmost = AlwaysOnTopCheck.IsChecked == true;
        try
        {
            var updated = _desktopPreferences with { AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true };
            _desktopStore?.Save(updated);
            _desktopPreferences = updated;
            SetOutcome("Pencere tercihi kaydedildi.", true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetOutcome("Pencere tercihi uygulandı ancak kaydedilemedi.", false);
        }
    }

    private async Task InitializeAutoStartAsync()
    {
        if (_autoStartService is null || _desktopStore is null) return;
        try { await _autoStartService.ApplyAsync(_desktopPreferences.AutoStartWithGame); }
        catch (Exception error)
        {
            GameAutoStartService.RecordFailure(error);
            SetOutcome("Otomatik başlatma hazırlanamadı; Ayarlar'dan kapatıp yeniden açmayı dene.", false);
        }
        finally { AutoStartCheck.IsEnabled = true; }
    }

    private void AutoClose_Click(object sender, RoutedEventArgs e)
    {
        if (_desktopStore is null) return;
        try
        {
            var updated = _desktopPreferences with { AutoCloseWithGame = AutoCloseCheck.IsChecked == true };
            _desktopStore.Save(updated);
            _desktopPreferences = updated;
            AutoClosePreferenceChanged?.Invoke(this, EventArgs.Empty);
            SetOutcome(updated.AutoCloseWithGame ? "Otomatik kapatma açık; ETS2 kapandığında trainer kapanacak."
                : "Otomatik kapatma kapalı; trainer oyun kapandıktan sonra açık kalacak.", true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            AutoCloseCheck.IsChecked = _desktopPreferences.AutoCloseWithGame;
            SetOutcome("Otomatik kapatma kaydedilemedi; önceki tercih korunuyor.", false);
        }
    }

    private async void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        if (_autoStartService is null || _desktopStore is null) return;
        var previous = _desktopPreferences.AutoStartWithGame;
        var enabled = AutoStartCheck.IsChecked == true;
        AutoStartCheck.IsEnabled = false;
        var saved = false;
        try
        {
            var updated = _desktopPreferences with { AutoStartWithGame = enabled };
            _desktopStore.Save(updated);
            _desktopPreferences = updated;
            saved = true;
            await _autoStartService.ApplyAsync(enabled);
            SetOutcome(enabled ? "Otomatik başlatma açık; ETS2 açıldığında trainer başlatılacak."
                : "Otomatik başlatma kapalı; arka plan izleyicisi durduruldu.", true);
        }
        catch (Exception error)
        {
            GameAutoStartService.RecordFailure(error);
            if (saved)
            {
                try
                {
                    var restored = _desktopPreferences with { AutoStartWithGame = previous };
                    _desktopStore.Save(restored);
                    _desktopPreferences = restored;
                    await _autoStartService.ApplyAsync(previous);
                }
                catch (Exception rollbackError) { GameAutoStartService.RecordFailure(rollbackError); }
            }
            AutoStartCheck.IsChecked = _desktopPreferences.AutoStartWithGame;
            SetOutcome("Otomatik başlatma değiştirilemedi; ayar klasörünü ve Windows başlangıç izinlerini kontrol et.", false);
        }
        finally { AutoStartCheck.IsEnabled = true; }
    }

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = DesktopPreferencesStore.DirectoryPath;
            Directory.CreateDirectory(directory);
            _openSettingsDirectory(directory);
            SetOutcome("Ayar klasörü açıldı.", true);
        }
        catch (Exception error)
        {
            SetOutcome($"Ayar klasörü açılamadı: {error.Message}", false);
        }
    }

    private static void OpenShellDirectory(string directory) => Process.Start(new ProcessStartInfo
    {
        FileName = directory,
        UseShellExecute = true
    });
}
