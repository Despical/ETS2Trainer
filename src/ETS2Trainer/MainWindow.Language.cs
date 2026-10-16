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

using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using ETS2Trainer.Localization;

namespace ETS2Trainer;

public partial class MainWindow
{
    private static UiLanguage L => UiLanguage.Current;
    private readonly LanguagePreferencesStore? _languageStore;
    private bool _languageReady;
    private string _outcomeMessage = "ETS2 bağlantısı bekleniyor.";
    private string _lastUpdateMessage = "Henüz güncellenmedi";
    private DateTime? _lastUpdateAt;

    private void InitializeLanguagePresentation()
    {
        TurkishLanguageOption.IsChecked = !L.IsEnglish;
        EnglishLanguageOption.IsChecked = L.IsEnglish;
        _languageReady = true;
        L.PropertyChanged += LanguageChanged;
        Closed += (_, _) => L.PropertyChanged -= LanguageChanged;
        _activity.CollectionChanged += (_, _) => UpdateHistoryCount();
        UpdateHistoryCount();
    }

    private void LanguageOption_Checked(object sender, RoutedEventArgs e)
    {
        if (!_languageReady || sender is not RadioButton { Tag: string language } || L.Language == language) return;
        CancelMenuHotkeyCapture();
        L.SetLanguage(language);
        try
        {
            _languageStore?.Save(language);
            SetOutcome("Dil tercihi kaydedildi.", true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetOutcome("Dil değişti ancak tercih kaydedilemedi; yeniden açılışta hatırlanmayabilir.", false);
        }
    }

    private void LanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UiLanguage.Language) || !_languageReady || _closed) return;
        var outcome = _outcomeMessage;
        ApplySnapshot(_snapshot with { Notices = [] });
        OutcomeText.Text = L.Message(outcome);
        OutcomeText.ToolTip = OutcomeText.Text;
        TurkishLanguageOption.IsChecked = !L.IsEnglish;
        EnglishLanguageOption.IsChecked = L.IsEnglish;
        foreach (var item in _activity) item.RefreshLanguage();
        UpdateHistoryCount();
        UpdateLastRead();
        SetMenuHotkey(_menuHotkey);
        UpdateCaptionLanguage();
    }

    private void UpdateCaptionLanguage()
    {
        HideMenuButton.Content = L.Text(IsOverlayMode ? "Oyuna dön" : "Menüyü gizle");
        var minimize = L.Text(IsOverlayMode ? "Menüyü gizle" : "Simge durumuna küçült");
        var close = L.Text(IsOverlayMode ? "Oyuna dön" : "Kapat");
        MinimizeButton.ToolTip = minimize;
        CloseButton.ToolTip = close;
        AutomationProperties.SetName(MinimizeButton, minimize);
        AutomationProperties.SetName(CloseButton, close);
        Window_StateChanged(this, EventArgs.Empty);
    }

    private void UpdateHistoryCount() => HistoryCountText.Text = L.Format("{0} olay", _activity.Count);
    private void UpdateLastRead()
    {
        LastUpdateText.Text = _lastUpdateAt is { } time ? L.Format("Son okuma {0}", time.ToString("HH:mm:ss")) : L.Text(_lastUpdateMessage);
    }
    private void MarkRead()
    {
        _lastUpdateAt = DateTime.Now;
        UpdateLastRead();
    }

    private sealed class ActivityEntry(string message) : INotifyPropertyChanged
    {
        private readonly DateTime _time = DateTime.Now;
        public string Text => $"{_time:HH:mm:ss}  {L.Message(message)}";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void RefreshLanguage() => PropertyChanged?.Invoke(this, new(nameof(Text)));
    }
}
