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
using System.Text.Json;

namespace ETS2Trainer.Core;

public sealed record ProtectionPreferences(bool NoDamage = false, bool InfiniteFuel = false, bool NoFatigue = false);

public interface IProtectionPreferencesStore
{
    ProtectionPreferences Load();
    void Save(ProtectionPreferences preferences);
}

public sealed class ProtectionPreferencesStore(string? path = null) : IProtectionPreferencesStore
{
    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETS2Trainer", "protection.json");

    public ProtectionPreferences Load()
    {
        if (!File.Exists(Path)) return new();
        if (new FileInfo(Path).Length > 16_384) throw new InvalidDataException("Koruma ayar dosyası çok büyük.");
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path));
        if (settings is not { Version: 1 }) throw new InvalidDataException("Koruma ayar dosyası tanınmadı.");
        return new(settings.NoDamage, settings.InfiniteFuel, settings.NoFatigue);
    }

    public void Save(ProtectionPreferences preferences)
    {
        var destination = System.IO.Path.GetFullPath(Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Settings(1, preferences.NoDamage, preferences.InfiniteFuel, preferences.NoFatigue),
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

    private sealed record Settings(int Version, bool NoDamage = false, bool InfiniteFuel = false, bool NoFatigue = false);
}
