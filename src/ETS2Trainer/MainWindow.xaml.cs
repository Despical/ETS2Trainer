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

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ETS2Trainer.Core;
using ETS2Trainer.Localization;
using System.IO;
using System.Text.Json;

namespace ETS2Trainer;

public partial class MainWindow : Window
{
    private const long MaximumMoney = 9_000_000_000_000_000;
    private readonly ITrainerBackend _backend;
    private readonly DispatcherTimer _refreshTimer;
    private readonly SemaphoreSlim _backendGate = new(1, 1);
    private readonly ObservableCollection<ActivityEntry> _activity = [];
    private readonly Dictionary<TrainerFeature, FrameworkElement[]> _featureControls;
    private TrainerSnapshot _snapshot = new();
    private bool _executing;
    private bool _closed;
    private string _lastConnectionStatus = "";
    private string _lastRefreshError = "";

    public MainWindow(ITrainerBackend backend, LanguagePreferencesStore? languageStore = null,
        DesktopPreferencesStore? desktopStore = null, Action<string>? openSettingsDirectory = null,
        IGameAutoStartService? autoStartService = null, GameDebugSettingsService? debugSettings = null,
        string? gameDirectory = null)
    {
        _backend = backend;
        _languageStore = languageStore;
        _desktopStore = desktopStore;
        _autoStartService = autoStartService;
        _openSettingsDirectory = openSettingsDirectory ?? OpenShellDirectory;
        string? languageWarning = null;
        if (languageStore is not null)
        {
            try { L.SetLanguage(languageStore.Load()); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                L.SetLanguage("tr");
                languageWarning = "Dil ayarı okunamadı; Türkçe kullanılıyor.";
            }
        }
        InitializeComponent();
        _debugSettings = debugSettings ?? new GameDebugSettingsService();
        _gameDirectory = gameDirectory ?? GameDebugSettingsService.DefaultDirectory;
        InitializeOverlayPresentation();
        ActivityList.ItemsSource = _activity;
        _featureControls = new()
        {
            [TrainerFeature.RepairAll] = [RepairAllButton],
            [TrainerFeature.RepairEngine] = [RepairEngineButton],
            [TrainerFeature.RepairTransmission] = [RepairTransmissionButton],
            [TrainerFeature.RepairCabin] = [RepairCabinButton],
            [TrainerFeature.RepairChassis] = [RepairChassisButton],
            [TrainerFeature.RepairWheels] = [RepairWheelsButton],
            [TrainerFeature.RepairTrailer] = [RepairTrailerButton],
            [TrainerFeature.NoDamage] = [NoDamageCheck],
            [TrainerFeature.FillFuel] = [FillFuelButton],
            [TrainerFeature.InfiniteFuel] = [InfiniteFuelCheck],
            [TrainerFeature.Money] = [MoneyInput, MoneyButton],
            [TrainerFeature.Experience] = [ExperienceInput, ExperienceButton],
            [TrainerFeature.Time] = [DayButton, NightButton, HourInput, MinuteInput, TimeButton],
            [TrainerFeature.NoFatigue] = [NoFatigueCheck]
        };

        MoneyInput.KeyDown += MoneyInput_KeyDown;
        ExperienceInput.KeyDown += ExperienceInput_KeyDown;
        HourInput.KeyDown += TimeInput_KeyDown;
        MinuteInput.KeyDown += TimeInput_KeyDown;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        Loaded += Window_Loaded;
        Closed += (_, _) =>
        {
            _closed = true;
            _refreshTimer.Stop();
        };

        InitializeLanguagePresentation();
        InitializeDesktopPreferences();
        InitializeDebugSettings();
        ApplySnapshot(_snapshot);
        AddActivity("Kontrol paneli hazır. ETS2 bağlantısı bekleniyor.");
        if (languageWarning is not null) AddActivity(languageWarning);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        if (!_closed)
            _refreshTimer.Start();
        await InitializeAutoStartAsync();
    }

    private async Task RefreshAsync()
    {
        if (_closed || _executing || !await _backendGate.WaitAsync(0))
            return;

        try
        {
            var snapshot = await _backend.RefreshAsync();
            if (_closed)
                return;

            ApplySnapshot(snapshot);
            _lastRefreshError = "";
            MarkRead();
        }
        catch (Exception exception)
        {
            if (_closed)
                return;

            ApplySnapshot(new TrainerSnapshot { Status = "Bağlantı okunamadı" });
            var message = $"Okuma başarısız: {exception.Message}";
            SetOutcome(message, false);
            if (_lastRefreshError != message)
                AddActivity(message);
            _lastRefreshError = message;
        }
        finally
        {
            _backendGate.Release();
        }
    }

    private void ApplySnapshot(TrainerSnapshot snapshot)
    {
        _snapshot = snapshot;
        ConnectionText.Text = L.Message(snapshot.Status);
        ConnectionText.ToolTip = ConnectionText.Text;
        ConnectionDot.Fill = new SolidColorBrush(snapshot.Connected
            ? Color.FromRgb(113, 206, 158)
            : Color.FromRgb(140, 152, 165));
        VersionText.Text = L.Format("Sürüm {0} · PID {1}", string.IsNullOrWhiteSpace(snapshot.Version) ? "—" : snapshot.Version,
            snapshot.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "—");
        TruckText.Text = L.Message(string.IsNullOrWhiteSpace(snapshot.Truck)
            ? snapshot.Connected ? "Araç bilgisi henüz okunamadı" : "Araç bilgisi bekleniyor"
            : snapshot.Truck);
        FuelValue.Text = Percent(snapshot.FuelPercent);
        EngineValue.Text = Percent(snapshot.EngineDamage);
        TrailerValue.Text = Percent(snapshot.TrailerDamage);
        CargoDamageValue.Text = Percent(snapshot.CargoDamage);
        RepairEngineLabel.Text = $"{L.Text("Motor")}  ·  {Percent(snapshot.EngineDamage)}";
        RepairTransmissionLabel.Text = $"{L.Text("Şanzıman")}  ·  {Percent(snapshot.TransmissionDamage)}";
        RepairCabinLabel.Text = $"{L.Text("Kabin")}  ·  {Percent(snapshot.CabinDamage)}";
        RepairChassisLabel.Text = $"{L.Text("Şasi")}  ·  {Percent(snapshot.ChassisDamage)}";
        RepairWheelsLabel.Text = $"{L.Text("Lastikler")}  ·  {Percent(snapshot.WheelsDamage)}";
        RepairTrailerLabel.Text = $"{L.Text("Dorse")}  ·  {Percent(snapshot.TrailerDamage)}";
        MoneyValue.Text = snapshot.Money is { } money ? $"€ {money.ToString("N0", L.Culture)}" : "—";
        ExperienceValue.Text = snapshot.Experience is { } experience ? $"{experience.ToString("N0", L.Culture)} XP" : "—";
        LevelValue.Text = snapshot.Level?.ToString("N0", L.Culture) ?? "—";
        TimeValue.Text = snapshot.TimeMinutes is { } time && time >= 0
            ? $"{time / 60 % 24:00}:{time % 60:00}"
            : "— : —";
        NoDamageCheck.IsChecked = snapshot.NoDamage;
        InfiniteFuelCheck.IsChecked = snapshot.InfiniteFuel;
        NoFatigueCheck.IsChecked = snapshot.NoFatigue;
        UpdateDebugAvailability();
        UpdateAvailability();

        var connectionStatus = $"{snapshot.Connected}|{snapshot.Status}|{snapshot.ProcessId}|{snapshot.Version}";
        if (_lastConnectionStatus != connectionStatus)
        {
            AddActivity(snapshot.Status);
            SetOutcome(snapshot.Status, snapshot.Connected ? true : null);
            _lastConnectionStatus = connectionStatus;
        }
        foreach (var notice in snapshot.Notices)
        {
            AddActivity(notice);
            SetOutcome(notice, null);
        }
    }

    private void UpdateAvailability()
    {
        foreach (var (feature, controls) in _featureControls)
        {
            var status = GetFeature(feature);
            foreach (var control in controls)
            {
                control.IsEnabled = status.Available && !_executing;
                control.ToolTip = L.Message(_executing && status.Available ? "İşlem tamamlanıyor…"
                    : !string.IsNullOrWhiteSpace(status.Reason) ? status.Reason
                    : status.Available ? FeatureDescription(feature) : FeatureReason(feature));
            }
        }

        MoneyReason.Text = AvailabilityText(TrainerFeature.Money, "Bakiye, yazdığınız toplam değer olarak ayarlanır.");
        ExperienceReason.Text = AvailabilityText(TrainerFeature.Experience, "Deneyim, yazdığınız toplam XP olarak ayarlanır.");
        TimeReason.Text = AvailabilityText(TrainerFeature.Time, "Saat ileri alınır; aktif iş süreleri etkilenebilir.");
        FatigueReason.Text = AvailabilityText(TrainerFeature.NoFatigue, "Açıkken sürücü yorgunluğu kontrol altında tutulur.");
        var damageStatus = GetFeature(TrainerFeature.NoDamage);
        NoDamageDescriptionText.Text = L.Message(damageStatus.Available && !string.IsNullOrWhiteSpace(damageStatus.Reason)
            ? damageStatus.Reason : "Hasar korumasını etkin tutar.");
        NoDamageDescriptionText.ToolTip = NoDamageDescriptionText.Text;
    }

    private FeatureStatus GetFeature(TrainerFeature feature) =>
        _snapshot.Features.TryGetValue(feature, out var status)
            ? status
            : new FeatureStatus(false, _snapshot.Connected
                ? "Bu özellik için doğrulanmış adres bulunamadı."
                : "Özelliği kullanmak için ETS2 bağlantısı bekleniyor.");

    private string FeatureReason(TrainerFeature feature)
    {
        var status = GetFeature(feature);
        return string.IsNullOrWhiteSpace(status.Reason)
            ? "Bu özellik mevcut oyun durumunda kullanılamıyor."
            : status.Reason;
    }

    private string AvailabilityText(TrainerFeature feature, string availableText) =>
        L.Message(GetFeature(feature).Available ? availableText : FeatureReason(feature));

    private static string FeatureDescription(TrainerFeature feature) => feature switch
    {
        TrainerFeature.RepairAll => "Desteklenen araç, dorse ve yük hasarlarını onar.",
        TrainerFeature.FillFuel => "Mevcut aracın deposunu doldur.",
        TrainerFeature.Money => "Yeni bakiyeyi 0 ile 9000000000000000 arasında tam sayı olarak girin.",
        TrainerFeature.Experience => "Yeni XP değerini 0 ile 2147483647 arasında tam sayı olarak girin.",
        TrainerFeature.Time => "Saat 0–23, dakika 0–59. Saat ileri alınır; aktif iş süreleri etkilenebilir.",
        TrainerFeature.NoDamage => "Hasar korumasını aç veya kapat.",
        TrainerFeature.InfiniteFuel => "Sınırsız yakıtı aç veya kapat.",
        TrainerFeature.NoFatigue => "Yorgunluk kontrolünü aç veya kapat.",
        _ => "Bu parçanın hasarını onar."
    };

    private static string Percent(float? value) => value is { } percent && float.IsFinite(percent)
        ? L.IsEnglish ? $"{percent.ToString("0.#", L.Culture)}%" : $"%{percent.ToString("0.#", L.Culture)}"
        : "—";

    private async Task ExecuteAsync(TrainerFeature feature, double value = 0)
    {
        if (_closed || _executing)
            return;
        if (!GetFeature(feature).Available)
        {
            SetOutcome(FeatureReason(feature), false);
            return;
        }

        _executing = true;
        UpdateAvailability();
        SetOutcome("İşlem uygulanıyor…", null);
        await _backendGate.WaitAsync();
        try
        {
            if (_closed)
                return;

            var result = await _backend.ExecuteAsync(feature, value);
            if (_closed)
                return;

            SetOutcome(result.Message, result.Success);
            AddActivity($"{(result.Success ? "Tamamlandı" : "Uygulanamadı")} · {result.Message}");
            try
            {
                var snapshot = await _backend.RefreshAsync();
                if (!_closed)
                {
                    ApplySnapshot(snapshot);
                    MarkRead();
                }
            }
            catch (Exception exception)
            {
                if (!_closed)
                {
                    ApplySnapshot(new TrainerSnapshot { Status = "İşlem sonrası bağlantı okunamadı" });
                    AddActivity($"Güncelleme başarısız · {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            if (!_closed)
            {
                var message = $"İşlem başarısız: {exception.Message}";
                SetOutcome(message, false);
                AddActivity(message);
            }
        }
        finally
        {
            _backendGate.Release();
            _executing = false;
            if (!_closed)
                UpdateAvailability();
        }
    }

    private void AddActivity(string message)
    {
        _activity.Insert(0, new ActivityEntry(message));
        while (_activity.Count > 100)
            _activity.RemoveAt(_activity.Count - 1);
    }

    private void SetOutcome(string message, bool? success)
    {
        _outcomeMessage = message;
        OutcomeText.Text = L.Message(message);
        OutcomeText.ToolTip = OutcomeText.Text;
        OutcomeText.Foreground = new SolidColorBrush(success switch
        {
            true => Color.FromRgb(113, 206, 158),
            false => Color.FromRgb(246, 158, 135),
            _ => Color.FromRgb(243, 182, 90)
        });
    }

    private void AlwaysOnTop_Click(object sender, RoutedEventArgs e) => SaveAlwaysOnTop();
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (!TryDismissOverlay())
            SystemCommands.MinimizeWindow(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!TryDismissOverlay())
            SystemCommands.CloseWindow(this);
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (IsOverlayMode)
            return;
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowFrame is null)
            return;
        if (IsOverlayMode)
        {
            WindowFrame.Padding = new Thickness(0);
            return;
        }

        var maximized = WindowState == WindowState.Maximized;
        WindowFrame.Padding = maximized ? new Thickness(7) : new Thickness(0);
        MaximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        var description = L.Text(maximized ? "Önceki boyuta dön" : "Ekranı kapla");
        MaximizeButton.ToolTip = description;
        AutomationProperties.SetName(MaximizeButton, description);
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e) => _activity.Clear();
    private async void RepairAll_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(TrainerFeature.RepairAll);
    private async void FillFuel_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(TrainerFeature.FillFuel);
    private async void Day_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(TrainerFeature.Time, 12 * 60);
    private async void Night_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(TrainerFeature.Time, 0);

    private async void RepairPart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string featureName } && Enum.TryParse<TrainerFeature>(featureName, out var feature))
            await ExecuteAsync(feature);
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string featureName } toggle ||
            !Enum.TryParse<TrainerFeature>(featureName, out var feature))
            return;

        var desired = toggle.IsChecked == true;
        toggle.IsChecked = feature switch
        {
            TrainerFeature.NoDamage => _snapshot.NoDamage,
            TrainerFeature.InfiniteFuel => _snapshot.InfiniteFuel,
            TrainerFeature.NoFatigue => _snapshot.NoFatigue,
            _ => false
        };
        await ExecuteAsync(feature, desired ? 1 : 0);
    }

    private async void Money_Click(object sender, RoutedEventArgs e) => await ApplyMoneyAsync();
    private async void Experience_Click(object sender, RoutedEventArgs e) => await ApplyExperienceAsync();
    private async void Time_Click(object sender, RoutedEventArgs e) => await ApplyTimeAsync();

    private async Task ApplyMoneyAsync()
    {
        if (!long.TryParse(MoneyInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < 0 || value > MaximumMoney)
        {
            SetOutcome("Bakiye için 0 ile 9000000000000000 arasında bir tam sayı girin.", false);
            MoneyInput.Focus();
            return;
        }
        await ExecuteAsync(TrainerFeature.Money, value);
    }

    private async Task ApplyExperienceAsync()
    {
        if (!int.TryParse(ExperienceInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            SetOutcome("XP için 0 ile 2147483647 arasında bir tam sayı girin.", false);
            ExperienceInput.Focus();
            return;
        }
        await ExecuteAsync(TrainerFeature.Experience, value);
    }

    private async Task ApplyTimeAsync()
    {
        if (!int.TryParse(HourInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(MinuteInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || hours is < 0 or > 23 || minutes is < 0 or > 59)
        {
            SetOutcome("Saat 0–23, dakika 0–59 aralığında olmalı.", false);
            HourInput.Focus();
            return;
        }
        await ExecuteAsync(TrainerFeature.Time, hours * 60 + minutes);
    }

    private async void MoneyInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await ApplyMoneyAsync(); }
    }

    private async void ExperienceInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await ApplyExperienceAsync(); }
    }

    private async void TimeInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await ApplyTimeAsync(); }
    }
}
