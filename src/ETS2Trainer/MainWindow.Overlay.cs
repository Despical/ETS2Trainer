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

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using ETS2Trainer.Core;

namespace ETS2Trainer;

public partial class MainWindow
{
    public static readonly DependencyProperty IsOverlayModeProperty = DependencyProperty.Register(
        nameof(IsOverlayMode), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    private DesktopPresentation? _desktopPresentation;
    private string _overlayHint = "Oyun içinde seçtiğin tuşla menüyü açıp kapatabilirsin.";
    private MenuHotkey _menuHotkey = MenuHotkey.Default;

    public bool IsOverlayMode
    {
        get => (bool)GetValue(IsOverlayModeProperty);
        private set => SetValue(IsOverlayModeProperty, value);
    }

    public event EventHandler? OverlayDismissRequested;
    public event Func<MenuHotkey, HotkeyUpdateResult>? MenuHotkeyChangeRequested;
    internal void RecordOverlayActivity(string message) => AddActivity(message);

    /// <summary>Applies presentation only. The coordinator owns visibility and native focus.</summary>
    public void EnterOverlay(Rect bounds)
    {
        Dispatcher.VerifyAccess();
        if (bounds.IsEmpty || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Overlay bounds must be finite and positive.");

        if (!IsOverlayMode)
        {
            var normalBounds = WindowState == WindowState.Normal
                ? new Rect(FiniteOr(Left, 0), FiniteOr(Top, 0), ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height)
                : RestoreBounds;
            _desktopPresentation = new DesktopPresentation(normalBounds, WindowState, Topmost, ShowInTaskbar,
                ResizeMode, SizeToContent, MinWidth, MinHeight, MaxWidth, MaxHeight,
                WindowChrome.GetWindowChrome(this));
            IsOverlayMode = true;
            WindowState = WindowState.Normal;
        }

        SizeToContent = SizeToContent.Manual;
        MinWidth = Math.Min(760, bounds.Width);
        MinHeight = Math.Min(520, bounds.Height);
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        // Keep the existing controls responsive at their desktop minimum and scale once.
        // Refresh, actions, selected tab and backend all remain on this same window.
        var scale = Math.Min(1, Math.Min(bounds.Width / 980, bounds.Height / 720));
        WindowFrame.Width = bounds.Width / scale;
        WindowFrame.Height = bounds.Height / scale;
        WindowFrame.HorizontalAlignment = HorizontalAlignment.Left;
        WindowFrame.VerticalAlignment = VerticalAlignment.Top;
        WindowFrame.Padding = new Thickness(0);
        WindowFrame.LayoutTransform = new ScaleTransform(scale, scale);
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 44 * scale,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });
        MaximizeButton.Visibility = Visibility.Collapsed;
        AlwaysOnTopCheck.Visibility = Visibility.Collapsed;
        UpdateCaptionLanguage();
        UpdateOverlayHint();
    }

    public void ExitOverlay()
    {
        Dispatcher.VerifyAccess();
        CancelMenuHotkeyCapture();
        CancelCameraKeyCapture();
        if (!IsOverlayMode || _desktopPresentation is not { } desktop)
            return;

        IsOverlayMode = false;
        WindowFrame.ClearValue(WidthProperty);
        WindowFrame.ClearValue(HeightProperty);
        WindowFrame.ClearValue(HorizontalAlignmentProperty);
        WindowFrame.ClearValue(VerticalAlignmentProperty);
        WindowFrame.ClearValue(LayoutTransformProperty);
        WindowFrame.Padding = new Thickness(0);
        WindowChrome.SetWindowChrome(this, desktop.Chrome);
        WindowState = WindowState.Normal;
        MinWidth = desktop.MinWidth;
        MinHeight = desktop.MinHeight;
        MaxWidth = desktop.MaxWidth;
        MaxHeight = desktop.MaxHeight;
        ResizeMode = desktop.ResizeMode;
        ShowInTaskbar = desktop.ShowInTaskbar;
        Topmost = desktop.Topmost;
        Left = desktop.Bounds.Left;
        Top = desktop.Bounds.Top;
        Width = desktop.Bounds.Width;
        Height = desktop.Bounds.Height;
        SizeToContent = desktop.SizeToContent;
        WindowState = desktop.WindowState;
        AlwaysOnTopCheck.IsChecked = desktop.Topmost;
        AlwaysOnTopCheck.Visibility = Visibility.Visible;
        MaximizeButton.Visibility = Visibility.Visible;
        UpdateCaptionLanguage();
        Window_StateChanged(this, EventArgs.Empty);
        _desktopPresentation = null;
        UpdateOverlayHint();
    }

    public void SetOverlayHint(string hint)
    {
        Dispatcher.VerifyAccess();
        _overlayHint = hint ?? "";
        UpdateOverlayHint();
    }

    public void SetMenuHotkey(MenuHotkey key)
    {
        Dispatcher.VerifyAccess();
        _menuHotkey = key ?? throw new ArgumentNullException(nameof(key));
        OverlayKeyText.Text = L.Format("Geçerli tuş: {0}", key.DisplayName);
        UpdateMenuHotkeyCapturePresentation();
        UpdateOverlayHint();
    }

    private HotkeyUpdateResult ApplyMenuHotkey(MenuHotkey candidate)
    {
        HotkeyUpdateResult result;
        try
        {
            result = MenuHotkeyChangeRequested?.Invoke(candidate)
                ?? new HotkeyUpdateResult(false, "Kısayol yöneticisi hazır değil.");
        }
        catch (Exception error)
        {
            result = new HotkeyUpdateResult(false, $"Kısayol değiştirilemedi: {error.Message}");
        }
        if (result.Success)
            SetMenuHotkey(candidate);
        else
            SetMenuHotkey(_menuHotkey);
        SetOutcome(result.Message, result.Success);
        AddActivity(result.Message);
        SetOverlayHint(result.Message);
        return result;
    }

    private void InitializeOverlayPresentation()
    {
        SetMenuHotkey(MenuHotkey.Default);
        PreviewKeyDown += (_, e) =>
        {
            if (TryHandleCameraKeyDown(e))
                return;
            if (TryHandleMenuHotkeyCaptureKeyDown(e))
                return;
            if (IsOverlayMode && e.Key == Key.Escape)
            {
                e.Handled = true;
                RequestOverlayDismiss();
            }
        };
        PreviewKeyUp += MenuHotkeyCaptureKeyUp;
        PreviewKeyUp += CameraKeyUp;
        Deactivated += (_, _) => { CancelMenuHotkeyCapture(); CancelCameraKeyCapture(); };
        MainTabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, MainTabs) && !ReferenceEquals(MainTabs.SelectedItem, SettingsTab))
                CancelMenuHotkeyCapture();
            if (ReferenceEquals(e.Source, MainTabs) && !ReferenceEquals(MainTabs.SelectedItem, DebugTab))
                CancelCameraKeyCapture();
        };
        Closing += Overlay_WindowClosing;
    }

    private void Overlay_WindowClosing(object? sender, CancelEventArgs e)
    {
        CancelMenuHotkeyCapture();
        CancelCameraKeyCapture();
        if (!IsOverlayMode)
            return;
        e.Cancel = true;
        RequestOverlayDismiss();
    }

    private void HideMenu_Click(object sender, RoutedEventArgs e) => RequestOverlayDismiss();

    private void QuitTrainer_Click(object sender, RoutedEventArgs e) => QuitTrainer();

    internal void QuitTrainer()
    {
        ExitOverlay();
        Close();
    }

    private bool TryDismissOverlay()
    {
        if (!IsOverlayMode)
            return false;
        RequestOverlayDismiss();
        return true;
    }

    private void RequestOverlayDismiss() => OverlayDismissRequested?.Invoke(this, EventArgs.Empty);

    private void UpdateOverlayHint()
    {
        OverlayHintText.Text = L.Message(_overlayHint);
        OverlayHintText.ToolTip = OverlayHintText.Text;
        HideMenuButton.ToolTip = L.Format("Menüyü gizle. Oyunda {0} ile aç/kapat; Esc ile oyuna dön.", _menuHotkey.DisplayName);
    }

    private static double FiniteOr(double value, double fallback) => double.IsFinite(value) ? value : fallback;

    private sealed record DesktopPresentation(Rect Bounds, WindowState WindowState, bool Topmost, bool ShowInTaskbar,
        ResizeMode ResizeMode, SizeToContent SizeToContent, double MinWidth, double MinHeight,
        double MaxWidth, double MaxHeight, WindowChrome? Chrome);
}
