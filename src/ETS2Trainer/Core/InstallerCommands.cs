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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ETS2Trainer.Localization;
using Microsoft.Win32;

namespace ETS2Trainer.Core;

internal static class InstallerCommands
{
    internal const string SetupMutex = @"Local\ETS2Trainer.Installer";

    internal static bool IsCommand(string[] arguments) => arguments.Length > 0
        && arguments[0].StartsWith("--installer-", StringComparison.Ordinal);

    internal static async Task<int> RunAsync(string[] arguments)
    {
        try
        {
            switch (arguments)
            {
                case ["--installer-status"]:
                    return new DesktopPreferencesStore().Load().AutoStartWithGame ? 64 : 0;
                case ["--installer-stop"]:
                    await StopAsync();
                    return 0;
                case ["--installer-configure", "--auto-start=on" or "--auto-start=off", "--language=en" or "--language=tr"]:
                    await ConfigureAsync(arguments[1] == "--auto-start=on", arguments[2][11..]);
                    return 0;
                case ["--installer-uninstall"]:
                    await UninstallAsync();
                    return 0;
                case ["--installer-resume"]:
                    ResumeWatcher();
                    return 0;
                default:
                    return 2;
            }
        }
        catch (Exception error)
        {
            GameAutoStartService.RecordFailure(error);
            return 1;
        }
    }

    private static void ResumeWatcher()
    {
        if (!new DesktopPreferencesStore().Load().AutoStartWithGame) return;
        using var key = Registry.CurrentUser.OpenSubKey(GameAutoStartService.RunKey);
        var command = key?.GetValue(GameAutoStartService.StartupValueName) as string ?? "";
        var match = Regex.Match(command, "^\"([^\"]+)\" --watch-game$", RegexOptions.CultureInvariant);
        if (!match.Success) return;
        var executable = match.Groups[1].Value;
        if (!File.Exists(executable) || FileVersionInfo.GetVersionInfo(executable).ProductName != "ETS2Trainer") return;
        using var watcher = Process.Start(new ProcessStartInfo(executable, GameAutoStartService.WatchArgument + " --after-setup")
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static async Task ConfigureAsync(bool enabled, string language)
    {
        var store = new DesktopPreferencesStore();
        var previous = store.Load();
        store.Save(previous with { AutoStartWithGame = enabled });
        try
        {
            // Setup starts the watcher after releasing its installation mutex.
            await new GameAutoStartService().ConfigureAsync(enabled, startWatcher: false);
            var languageStore = new LanguagePreferencesStore();
            if (!File.Exists(languageStore.Path)) languageStore.Save(language);
        }
        catch
        {
            store.Save(previous);
            throw;
        }
    }

    private static async Task StopAsync()
    {
        await GameAutoStartService.StopWatcherAsync();
        if (!TrainerInstance.IsRunning("Desktop")) return;
        try
        {
            using var quit = EventWaitHandle.OpenExisting(TrainerInstance.Name("QuitDesktop"));
            quit.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Older portable releases have no IPC endpoint. Request their normal
            // window close; an open overlay may still require Settings > Quit.
            using var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName("ETS2Trainer"))
            {
                using (process)
                {
                    try
                    {
                        if (process.Id != current.Id && process.SessionId == current.SessionId
                            && process.MainModule?.FileVersionInfo.ProductName == "ETS2Trainer")
                            process.CloseMainWindow();
                    }
                    catch (Exception error) when (error is Win32Exception or InvalidOperationException) { }
                }
            }
        }
        if (!await Task.Run(() => TrainerInstance.WaitForExit("Desktop", TimeSpan.FromSeconds(10))))
            throw new IOException("Close the trainer through Settings > Quit trainer, then try again.");
    }

    private static async Task UninstallAsync()
    {
        var service = new GameAutoStartService();
        string? registered;
        using (var key = Registry.CurrentUser.OpenSubKey(GameAutoStartService.RunKey))
            registered = key?.GetValue(GameAutoStartService.StartupValueName) as string;
        var ownsRegistration = string.Equals(registered, service.StartupCommand, StringComparison.OrdinalIgnoreCase);
        var executable = Environment.ProcessPath!;
        using var current = Process.GetCurrentProcess();
        var ownProcessRunning = false;
        foreach (var process in Process.GetProcessesByName("ETS2Trainer"))
        {
            using (process)
            {
                try
                {
                    ownProcessRunning |= process.Id != current.Id && process.SessionId == current.SessionId
                        && string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException) { }
            }
        }
        if (ownsRegistration || ownProcessRunning) await StopAsync();
        if (ownsRegistration || registered is null)
        {
            var store = new DesktopPreferencesStore();
            store.Save(store.Load() with { AutoStartWithGame = false });
            // Do not remove a startup entry which belongs to another copy.
            if (ownsRegistration) await service.ConfigureAsync(false, startWatcher: false);
        }
    }

    internal static async Task<bool> WaitForSetupAsync()
    {
        var timer = Stopwatch.StartNew();
        while (Mutex.TryOpenExisting(SetupMutex, out var setup))
        {
            setup.Dispose();
            if (timer.Elapsed > TimeSpan.FromMinutes(2)) return false;
            await Task.Delay(200);
        }
        return true;
    }

    internal static bool IsSetupRunning()
    {
        if (!Mutex.TryOpenExisting(SetupMutex, out var setup)) return false;
        setup.Dispose();
        return true;
    }
}
