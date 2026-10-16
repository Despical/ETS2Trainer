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
using System.IO;
using System.Text.Json;
using System.Windows;
using ETS2Trainer.Core;

namespace ETS2Trainer;

public partial class MainWindow
{
    private readonly DesktopPreferencesStore? _desktopStore;
    private readonly Action<string> _openSettingsDirectory;

    private void InitializeDesktopPreferences()
    {
        SettingsDirectoryText.Text = DesktopPreferencesStore.DirectoryPath;
        SettingsDirectoryText.ToolTip = SettingsDirectoryText.Text;
        if (_desktopStore is null) return;
        try
        {
            var preferences = _desktopStore.Load();
            Topmost = preferences.AlwaysOnTop;
            AlwaysOnTopCheck.IsChecked = preferences.AlwaysOnTop;
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
            _desktopStore?.Save(new(AlwaysOnTopCheck.IsChecked == true));
            SetOutcome("Pencere tercihi kaydedildi.", true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetOutcome("Pencere tercihi uygulandı ancak kaydedilemedi.", false);
        }
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
