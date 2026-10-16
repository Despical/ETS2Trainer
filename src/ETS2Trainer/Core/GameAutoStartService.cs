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
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ETS2Trainer.Core;

public interface IGameAutoStartService
{
    Task ApplyAsync(bool enabled);
}

public sealed class GameAutoStartService : IGameAutoStartService
{
    public const string WatchArgument = "--watch-game";
    public const string StartupValueName = "ETS2Trainer.GameWatcher";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _executablePath;

    public GameAutoStartService(string? executablePath = null)
    {
        _executablePath = Path.GetFullPath(executablePath ?? Environment.ProcessPath
            ?? throw new IOException("Unable to locate the trainer executable."));
        if (_executablePath.IndexOfAny(['\"', '\r', '\n']) >= 0)
            throw new IOException("The trainer path cannot be used for automatic startup.");
    }

    public async Task ApplyAsync(bool enabled)
    {
        if (!enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(StartupValueName, throwOnMissingValue: false);
            await StopWatcherAsync();
            return;
        }

        var command = $"\"{_executablePath}\" {WatchArgument}";
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true))
        {
            if (!string.Equals(key.GetValue(StartupValueName) as string, command, StringComparison.Ordinal))
            {
                await StopWatcherAsync();
                key.SetValue(StartupValueName, command, RegistryValueKind.String);
            }
        }

        if (TrainerInstance.IsRunning("Watcher")) return;
        using var process = Start(WatchArgument, hidden: true);
        var ready = await Task.Run(async () =>
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.Elapsed < TimeSpan.FromSeconds(8))
            {
                if (TrainerInstance.IsRunning("Watcher")) return true;
                if (process.HasExited) return false;
                await Task.Delay(100);
            }
            return false;
        });
        if (!ready) throw new IOException("The automatic startup watcher could not start.");
    }

    private static async Task StopWatcherAsync()
    {
        try
        {
            using var stop = EventWaitHandle.OpenExisting(TrainerInstance.Name("StopWatcher"));
            stop.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        if (!await Task.Run(() => TrainerInstance.WaitForExit("Watcher", TimeSpan.FromSeconds(8))))
            throw new IOException("The automatic startup watcher did not stop in time.");
    }

    public async Task WatchAsync(DesktopPreferencesStore store)
    {
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, TrainerInstance.Name("StopWatcher"));
        stop.Reset();
        var launches = new GameLaunchTracker();
        var pendingLaunch = false;
        while (!stop.WaitOne(0))
        {
            if (!store.Load().AutoStartWithGame) return;
            var games = ReadGameProcesses();
            if (games is not null)
            {
                var newLaunch = launches.Observe(games);
                pendingLaunch = games.Count > 0 && (pendingLaunch || newLaunch);
                if (pendingLaunch)
                {
                    if (TrainerInstance.IsRunning("Desktop")) pendingLaunch = false;
                    else
                    {
                        try
                        {
                            using var trainer = Start(null, hidden: false);
                            pendingLaunch = false;
                        }
                        catch (Exception error) when (error is IOException or Win32Exception or InvalidOperationException)
                        {
                            RecordFailure(error);
                            await Task.Delay(TimeSpan.FromSeconds(3));
                        }
                    }
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(1.5));
        }
    }

    private Process Start(string? argument, bool hidden) => Process.Start(new ProcessStartInfo
    {
        FileName = _executablePath,
        Arguments = argument ?? "",
        WorkingDirectory = Path.GetDirectoryName(_executablePath)!,
        UseShellExecute = false,
        CreateNoWindow = hidden,
        WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
    }) ?? throw new IOException("Unable to start the trainer process.");

    private static List<GameProcessIdentity>? ReadGameProcesses()
    {
        var games = new List<GameProcessIdentity>();
        var complete = true;
        var processes = Process.GetProcessesByName("eurotrucks2");
        foreach (var process in processes)
        {
            using (process)
            {
                try { games.Add(new(process.Id, process.StartTime.ToUniversalTime().Ticks)); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    complete = false;
                }
            }
        }
        return complete ? games : null;
    }

    internal static void RecordFailure(Exception error)
    {
        try
        {
            Directory.CreateDirectory(DesktopPreferencesStore.DirectoryPath);
            File.WriteAllText(Path.Combine(DesktopPreferencesStore.DirectoryPath, "startup.log"),
                $"{DateTimeOffset.Now:O}\n{error.GetType().Name}: {error.Message}\n");
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
    }
}
