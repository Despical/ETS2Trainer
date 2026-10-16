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
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ETS2Trainer.Localization;

public sealed class UiLanguage : INotifyPropertyChanged
{
    public static UiLanguage Current { get; } = new();
    private readonly Dictionary<string, string> _english;
    private readonly (Regex Pattern, string English, int Count)[] _messages;
    private string _language = "tr";
    public string Language => _language;
    public bool IsEnglish => _language == "en";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "tr-TR");
    public event PropertyChangedEventHandler? PropertyChanged;

    private UiLanguage()
    {
        using var stream = typeof(UiLanguage).Assembly.GetManifestResourceStream("ETS2Trainer.Localization.en.json")
            ?? throw new InvalidDataException("Missing English translation resource.");
        _english = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        _messages = _english.Where(pair => pair.Key.Contains("{0}"))
            .OrderByDescending(pair => pair.Key.Length).Select(pair =>
            {
                var pattern = new StringBuilder("\\A");
                var position = 0;
                var count = 0;
                foreach (Match token in Regex.Matches(pair.Key, @"\{(\d+)\}"))
                {
                    pattern.Append(Regex.Escape(pair.Key[position..token.Index]));
                    var index = int.Parse(token.Groups[1].Value, CultureInfo.InvariantCulture);
                    pattern.Append($"(?<p{index}>.+?)");
                    count = Math.Max(count, index + 1);
                    position = token.Index + token.Length;
                }
                pattern.Append(Regex.Escape(pair.Key[position..])).Append("\\z");
                return (new Regex(pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline,
                    TimeSpan.FromMilliseconds(100)), pair.Value, count);
            }).ToArray();
    }

    public void SetLanguage(string language)
    {
        if (language is not ("tr" or "en")) throw new ArgumentException("Unsupported language.", nameof(language));
        if (_language == language) return;
        _language = language;
        PropertyChanged?.Invoke(this, new(nameof(Language)));
    }

    public bool HasEnglish(string text) => _english.ContainsKey(text);
    public string Text(string text) => IsEnglish && _english.TryGetValue(text, out var translated) ? translated : text;
    public string Format(string template, params object?[] args) => string.Format(Culture, Text(template), args);
    public string Message(string text) => TranslateMessage(text, 0);

    private string TranslateMessage(string text, int depth)
    {
        if (!IsEnglish || depth > 5) return text;
        if (_english.TryGetValue(text, out var translated)) return translated;
        foreach (var (pattern, english, count) in _messages)
        {
            var match = pattern.Match(text);
            if (!match.Success) continue;
            var arguments = Enumerable.Range(0, count).Select(index =>
                (object)TranslateMessage(match.Groups[$"p{index}"].Value, depth + 1)).ToArray();
            return string.Format(Culture, english, arguments);
        }
        return text;
    }
}

public sealed class LanguagePreferencesStore(string? path = null)
{
    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETS2Trainer", "language.json");
    public string Load()
    {
        if (!File.Exists(Path)) return "tr";
        if (new FileInfo(Path).Length > 4096) throw new InvalidDataException("Language settings file is too large.");
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path));
        if (settings is not { Version: 1, Language: "tr" or "en" }) throw new InvalidDataException("Invalid language settings.");
        return settings.Language;
    }
    public void Save(string language)
    {
        if (language is not ("tr" or "en")) throw new ArgumentException("Unsupported language.", nameof(language));
        var destination = System.IO.Path.GetFullPath(Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Settings(1, language), new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private sealed record Settings(int Version, string Language);
}
