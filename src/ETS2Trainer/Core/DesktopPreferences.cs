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

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ETS2Trainer.Core;

public sealed record DesktopPreferences(bool AlwaysOnTop = false, bool AutoStartWithGame = false);

public sealed class DesktopPreferencesStore(string? path = null)
{
    public static string DirectoryPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETS2Trainer");
    public string Path { get; } = path ?? System.IO.Path.Combine(DirectoryPath, "interface.json");

    public DesktopPreferences Load() => WithFileLock(Read);

    public void Save(DesktopPreferences preferences) => WithFileLock(() =>
    {
        Write(preferences);
        return true;
    });

    private T WithFileLock<T>(Func<T> action)
    {
        var identity = Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(Path).ToUpperInvariant());
        var name = "ETS2Trainer.Interface." + Convert.ToHexString(SHA256.HashData(identity));
        using var mutex = new Mutex(false, name);
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(3)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new IOException("Interface preferences are busy; try again.");
        try { return action(); }
        finally { mutex.ReleaseMutex(); }
    }

    private DesktopPreferences Read()
    {
        if (!File.Exists(Path)) return new();
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 4096) throw new InvalidDataException("Interface settings file is too large.");
        var settings = JsonSerializer.Deserialize<Settings>(stream);
        if (settings is not { Version: 1 }) throw new InvalidDataException("Invalid interface settings.");
        return new(settings.AlwaysOnTop, settings.AutoStartWithGame);
    }

    private void Write(DesktopPreferences preferences)
    {
        var destination = System.IO.Path.GetFullPath(Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Settings(1, preferences.AlwaysOnTop, preferences.AutoStartWithGame),
                new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record Settings(int Version, bool AlwaysOnTop = false, bool AutoStartWithGame = false);
}
