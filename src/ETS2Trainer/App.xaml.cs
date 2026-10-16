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
using ETS2Trainer.Core;

namespace ETS2Trainer;

public partial class App : Application
{
    private ITrainerBackend? _backend;
    private OverlayController? _overlay;
    private TrainerInstance? _instance;
    private GameSessionController? _gameSession;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var watchMode = e.Args.Contains(GameAutoStartService.WatchArgument, StringComparer.Ordinal);
        _instance = TrainerInstance.TryAcquire(watchMode ? "Watcher" : "Desktop");
        if (_instance is null)
        {
            Shutdown();
            return;
        }
        var desktopStore = new DesktopPreferencesStore();
        var autoStart = new GameAutoStartService();
        if (watchMode)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { await autoStart.WatchAsync(desktopStore); }
            catch (Exception error) { GameAutoStartService.RecordFailure(error); }
            finally { Shutdown(); }
            return;
        }
        _backend = new SavedProtectionBackend(new TrainerBackend(), new ProtectionPreferencesStore());
        var window = new MainWindow(_backend, new Localization.LanguagePreferencesStore(), desktopStore,
            autoStartService: autoStart);
        MainWindow = window;
        window.Show();
        _overlay = new OverlayController(window);
        _gameSession = new GameSessionController(window);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _gameSession?.Dispose();
        _overlay?.Dispose();
        _backend?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
