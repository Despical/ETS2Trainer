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

public sealed record MenuHotkey(string Id, string DisplayName, uint VirtualKey)
{
    public static MenuHotkey Default { get; } = new("Insert", "Insert", 0x2d);
    public static IReadOnlyList<MenuHotkey> Choices { get; } = Array.AsReadOnly(new[]
    {
        Default, new("Home", "Home", 0x24), new("End", "End", 0x23),
        new("PageUp", "Page Up", 0x21), new("PageDown", "Page Down", 0x22),
        new("Pause", "Pause", 0x13), new("ScrollLock", "Scroll Lock", 0x91),
        new("Backspace", "Backspace", 0x08), new("Tab", "Tab", 0x09), new("Enter", "Enter", 0x0d),
        new("CapsLock", "Caps Lock", 0x14), new("Space", "Boşluk", 0x20), new("Delete", "Delete", 0x2e),
        new("Left", "Sol ok", 0x25), new("Up", "Yukarı ok", 0x26), new("Right", "Sağ ok", 0x27), new("Down", "Aşağı ok", 0x28),
        new("NumLock", "Num Lock", 0x90), new("Multiply", "Numpad ×", 0x6a), new("Add", "Numpad +", 0x6b),
        new("Subtract", "Numpad −", 0x6d), new("Decimal", "Numpad .", 0x6e), new("Divide", "Numpad /", 0x6f)
    }.Concat(Enumerable.Range(1, 24).Where(i => i != 12).Select(i => new MenuHotkey($"F{i}", $"F{i}", (uint)(0x6f + i))))
        .Concat(Enumerable.Range(0, 26).Select(i => new MenuHotkey(((char)('A' + i)).ToString(), ((char)('A' + i)).ToString(), (uint)(0x41 + i))))
        .Concat(Enumerable.Range(0, 10).Select(i => new MenuHotkey($"D{i}", i.ToString(), (uint)(0x30 + i))))
        .Concat(Enumerable.Range(0, 10).Select(i => new MenuHotkey($"NumPad{i}", $"Numpad {i}", (uint)(0x60 + i))))
        .Concat(new uint[] {0xba,0xbb,0xbc,0xbd,0xbe,0xbf,0xc0,0xdb,0xdc,0xdd,0xde,0xdf,0xe2}
            .Select(key => new MenuHotkey($"VK_{key:X2}", "Noktalama tuşu", key))).ToArray());

    public static bool TryFromVirtualKey(uint virtualKey, out MenuHotkey key)
    {
        var found = Choices.FirstOrDefault(candidate => candidate.VirtualKey == virtualKey);
        key = found ?? Default;
        return found is not null;
    }

    public static string CleanDisplayName(string? value, string fallback) => string.IsNullOrWhiteSpace(value)
        || value.Length > 40 || value.Any(char.IsControl) ? fallback : value.Trim();

    public static bool TryFind(string? id, out MenuHotkey key)
    {
        var found = Choices.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));
        key = found ?? Default;
        return found is not null;
    }
}

public sealed record HotkeyUpdateResult(bool Success, string Message);
public sealed record HotkeyLoadResult(MenuHotkey Key, string Message = "");

public sealed class MenuHotkeyStore(string? settingsPath = null)
{
    public string SettingsPath { get; } = settingsPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETS2Trainer", "settings.json");

    public HotkeyLoadResult Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new(MenuHotkey.Default);
            if (new FileInfo(SettingsPath).Length > 16_384) throw new InvalidDataException("Ayar dosyası çok büyük.");
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            if (settings is { Version: 1 } && MenuHotkey.TryFind(settings.MenuHotkey, out var key))
                return new(key with { DisplayName = MenuHotkey.CleanDisplayName(settings.DisplayName, key.DisplayName) });
            return new(MenuHotkey.Default, "Kayıtlı menü tuşu tanınmadı; Insert kullanılıyor.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(MenuHotkey.Default, "Ayarlar okunamadı; menü tuşu Insert olarak açıldı.");
        }
    }

    public void Save(MenuHotkey key)
    {
        if (!MenuHotkey.TryFind(key.Id, out var canonical) || canonical.VirtualKey != key.VirtualKey)
            throw new ArgumentException("Desteklenen bir menü tuşu seçin.", nameof(key));
        var path = Path.GetFullPath(SettingsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var label = MenuHotkey.CleanDisplayName(key.DisplayName, canonical.DisplayName);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Settings(1, canonical.Id, label), new JsonSerializerOptions { WriteIndented = true });
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(bytes); file.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record Settings(int Version, string MenuHotkey, string? DisplayName = null);
}
