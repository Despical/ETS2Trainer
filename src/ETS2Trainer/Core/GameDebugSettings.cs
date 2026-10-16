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

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ETS2Trainer.Core;

public sealed record GameDebugProfile(string Name, string ControlsPath);
public sealed record GameDebugSettings(bool Developer, bool Console, bool FreeCamera, string CameraExpression);

public sealed class GameDebugSettingsService(Func<bool>? isGameRunning = null)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly Regex CameraBinding = new(@"(?m)^\s*config_lines\[\d+\]: ""mix camdbg `(?<expression>[^`\r\n]*)`""\s*$");
    private readonly Func<bool> _isGameRunning = isGameRunning ?? IsEts2Running;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Euro Truck Simulator 2");
    public bool GameRunning => _isGameRunning();

    public IReadOnlyList<GameDebugProfile> Profiles(string directory)
    {
        var profiles = new List<GameDebugProfile>();
        foreach (var source in new[] { "profiles", "steam_profiles" })
        {
            var root = Path.Combine(directory, source);
            if (!Directory.Exists(root)) continue;
            foreach (var profile in Directory.EnumerateDirectories(root))
            {
                var path = Path.Combine(profile, "controls.sii");
                if (!File.Exists(path)) continue;
                var name = Path.GetFileName(profile);
                try { name = Utf8.GetString(Convert.FromHexString(name)); }
                catch (Exception error) when (error is FormatException or DecoderFallbackException) { }
                profiles.Add(new($"{name} · {source}", path));
            }
        }
        return profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public GameDebugSettings Read(string directory, GameDebugProfile? profile)
    {
        var config = ReadText(Path.Combine(directory, "config.cfg"));
        var developer = ReadFlag(config, "g_developer");
        var console = ReadFlag(config, "g_console");
        var expression = profile is null ? "0" : ReadCamera(ReadText(ProfilePath(directory, profile)));
        return new(developer, console, developer && expression.Trim() != "0", expression);
    }

    public string? FindKeyConflict(GameDebugProfile profile, string token)
    {
        foreach (Match match in Regex.Matches(ReadText(profile.ControlsPath), @"mix (?<action>\w+) `(?<expression>[^`\r\n]*)`"))
        {
            if (match.Groups["action"].Value == "camdbg") continue;
            if (Regex.IsMatch(match.Groups["expression"].Value, @"\bkeyboard\." + Regex.Escape(token) + @"\?"))
                return match.Groups["action"].Value;
        }
        return null;
    }

    // Returns the backup paths. Re-read on save so unrelated edits are retained.
    public IReadOnlyList<string> Save(string directory, GameDebugProfile? profile, bool developer, bool console,
        bool freeCamera, string? cameraToken)
    {
        EnsureStopped();
        if ((console || freeCamera) && !developer)
            throw new InvalidDataException("Konsol ve serbest kamera için geliştirici modunu aç.");
        if (freeCamera && profile is null)
            throw new InvalidDataException("Önce bir oyun profili seç.");
        if (cameraToken is not null && !GameCameraKey.IsSupportedToken(cameraToken))
            throw new InvalidDataException("Bu kamera tuşu desteklenmiyor.");

        var configPath = Path.Combine(directory, "config.cfg");
        var changes = new List<FileChange>();
        var configBytes = File.ReadAllBytes(configPath);
        var config = Decode(configBytes);
        // Validate both flags before creating any backup or changing a file.
        ReadFlag(config, "g_developer");
        ReadFlag(config, "g_console");
        var updatedConfig = SetFlag(SetFlag(config, "g_developer", developer), "g_console", console);
        changes.Add(new(configPath, configBytes, Encode(updatedConfig, configBytes)));

        if (profile is not null)
        {
            var controlsPath = ProfilePath(directory, profile);
            var controlsBytes = File.ReadAllBytes(controlsPath);
            var controls = Decode(controlsBytes);
            var current = ReadCamera(controls);
            var expression = !freeCamera ? "0" : cameraToken is not null ? $"keyboard.{cameraToken}?0"
                : current.Trim() != "0" ? current : "keyboard.key0?0";
            if (freeCamera && expression != current && cameraToken is not null && FindKeyConflict(profile, cameraToken) is { } conflict)
                throw new InvalidDataException($"Seçilen tuş başka bir oyun kontrolünde kullanılıyor: {conflict}");
            var match = CameraBinding.Match(controls).Groups["expression"];
            var updated = controls[..match.Index] + expression + controls[(match.Index + match.Length)..];
            changes.Add(new(controlsPath, controlsBytes, Encode(updated, controlsBytes)));
        }

        changes.RemoveAll(c => c.Original.AsSpan().SequenceEqual(c.Updated));
        var applied = new List<FileChange>();
        try
        {
            foreach (var change in changes)
            {
                EnsureStopped();
                if (!File.ReadAllBytes(change.Path).AsSpan().SequenceEqual(change.Original))
                    throw new IOException("Oyun ayarları değişti; yeniden oku ve tekrar dene.");
                change.Backup = change.Path + ".ets2trainer-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".bak";
                Replace(change.Path, change.Updated, change.Backup);
                applied.Add(change);
                if (!File.ReadAllBytes(change.Path).AsSpan().SequenceEqual(change.Updated))
                    throw new IOException("Oyun ayarları kaydedildikten sonra doğrulanamadı.");
            }
        }
        catch (Exception saveError)
        {
            var errors = new List<Exception> { saveError };
            foreach (var change in applied.AsEnumerable().Reverse())
            {
                try
                {
                    if (!File.ReadAllBytes(change.Path).AsSpan().SequenceEqual(change.Updated))
                        throw new IOException($"Dosya başka bir işlemde değişti. Yedek: {change.Backup}");
                    Replace(change.Path, change.Original, null);
                }
                catch (Exception restoreError) { errors.Add(restoreError); }
            }
            if (errors.Count > 1) throw new AggregateException("Ayarlar geri yüklenemedi; .ets2trainer yedeklerini kontrol et.", errors);
            throw;
        }
        return applied.Select(c => c.Backup!).ToArray();
    }

    private void EnsureStopped()
    {
        if (GameRunning) throw new InvalidOperationException("Ayarları kaydetmeden önce ETS2'yi tamamen kapat. Kaydettikten sonra oyunu yeniden başlat.");
    }

    private static bool IsEts2Running()
    {
        var processes = Process.GetProcessesByName("eurotrucks2");
        try { return processes.Length != 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private static string ProfilePath(string directory, GameDebugProfile profile)
    {
        var path = Path.GetFullPath(profile.ControlsPath);
        var parent = Path.GetDirectoryName(Path.GetDirectoryName(path));
        if (Path.GetFileName(path) != "controls.sii" || !new[] { "profiles", "steam_profiles" }.Any(source =>
                string.Equals(parent, Path.GetFullPath(Path.Combine(directory, source)), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Seçilen profil oyun klasörüne ait değil.");
        return path;
    }

    private static string ReadText(string path) => Decode(File.ReadAllBytes(path));
    private static string Decode(byte[] bytes) => Utf8.GetString(bytes.AsSpan(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0));
    private static byte[] Encode(string text, byte[] original) => original.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })
        ? new byte[] { 0xef, 0xbb, 0xbf }.Concat(Utf8.GetBytes(text)).ToArray() : Utf8.GetBytes(text);

    private static bool ReadFlag(string config, string name)
    {
        var matches = Regex.Matches(config, @"(?m)^[ \t]*uset " + name + @" ""(?<value>[^""\r\n]*)""[ \t]*\r?$");
        if (matches.Count != 1 || matches[0].Groups["value"].Value is not ("0" or "1"))
            throw new InvalidDataException($"Oyun ayarı okunamadı: {name}");
        return matches[0].Groups["value"].Value == "1";
    }

    private static string SetFlag(string config, string name, bool enabled) =>
        Regex.Replace(config, @"(?m)^(?<prefix>[ \t]*uset " + name + @" "")[01](?<suffix>""[ \t]*\r?)$",
            m => m.Groups["prefix"].Value + (enabled ? "1" : "0") + m.Groups["suffix"].Value);

    private static string ReadCamera(string controls)
    {
        var matches = CameraBinding.Matches(controls);
        if (!controls.TrimStart().StartsWith("SiiNunit", StringComparison.Ordinal) || matches.Count != 1)
            throw new InvalidDataException("Serbest kamera ataması okunamadı; oyunda bu profilin klavye ayarlarını bir kez aç.");
        return matches[0].Groups["expression"].Value;
    }

    private static void Replace(string path, byte[] bytes, string? backup)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Replace(temporary, path, backup);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class FileChange(string path, byte[] original, byte[] updated)
    {
        public string Path { get; } = path;
        public byte[] Original { get; } = original;
        public byte[] Updated { get; } = updated;
        public string? Backup { get; set; }
    }
}

public static class GameCameraKey
{
    public static string? FromVirtualKey(uint key) => key switch
    {
        >= 0x30 and <= 0x39 => "key" + (key - 0x30),
        >= 0x41 and <= 0x5a => ((char)(key + 32)).ToString(),
        >= 0x70 and <= 0x7b => "f" + (key - 0x6f),
        >= 0x60 and <= 0x69 => "num" + (key - 0x60),
        0x20 => "space", 0x2d => "ins", 0x2e => "del", 0x24 => "home", 0x23 => "end",
        0x21 => "pgup", 0x22 => "pgdn", 0x25 => "larrow", 0x26 => "uarrow", 0x27 => "rarrow", 0x28 => "darrow",
        _ => null
    };

    public static bool IsSupportedToken(string token) => Enumerable.Range(0, 256).Any(k => FromVirtualKey((uint)k) == token);
}
