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

/// <summary>Keeps the user's preferences separate from the currently attached game's state.</summary>
public sealed class SavedProtectionBackend : ITrainerBackend
{
    private static readonly TrainerFeature[] SavedFeatures = [TrainerFeature.NoDamage, TrainerFeature.InfiniteFuel, TrainerFeature.NoFatigue];
    private readonly ITrainerBackend _inner;
    private readonly IProtectionPreferencesStore _store;
    private readonly Func<DateTime> _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<TrainerFeature, DateTime> _retryAfter = [];
    private readonly List<string> _notices = [];
    private ProtectionPreferences _desired;
    private int? _processId;
    private volatile bool _disposed;

    public SavedProtectionBackend(ITrainerBackend inner, IProtectionPreferencesStore store, Func<DateTime>? clock = null)
    {
        _inner = inner;
        _store = store;
        _clock = clock ?? (() => DateTime.UtcNow);
        try { _desired = store.Load(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            _desired = new();
            _notices.Add("Koruma tercihleri okunamadı; korumalar kapalı başlatıldı.");
        }
    }

    public async Task<TrainerSnapshot> RefreshAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return new() { Status = "Bağlantı kapatıldı" };
            var snapshot = await _inner.RefreshAsync().ConfigureAwait(false);
            if (_disposed) return snapshot;
            if (!snapshot.Connected || _processId != snapshot.ProcessId)
                _retryAfter.Clear();
            _processId = snapshot.Connected ? snapshot.ProcessId : null;

            var changed = false;
            foreach (var feature in SavedFeatures)
            {
                if (!snapshot.Connected || !snapshot.Features.TryGetValue(feature, out var status) || !status.Available)
                {
                    _retryAfter.Remove(feature);
                    continue;
                }
                var desired = feature switch
                {
                    TrainerFeature.NoDamage => _desired.NoDamage,
                    TrainerFeature.InfiniteFuel => _desired.InfiniteFuel,
                    _ => _desired.NoFatigue
                };
                var active = feature switch
                {
                    TrainerFeature.NoDamage => snapshot.NoDamage,
                    TrainerFeature.InfiniteFuel => snapshot.InfiniteFuel,
                    _ => snapshot.NoFatigue
                };
                if (!desired || active || _clock() < _retryAfter.GetValueOrDefault(feature)) continue;
                _retryAfter[feature] = _clock().AddSeconds(10);
                try
                {
                    var result = await _inner.ExecuteAsync(feature, 1).ConfigureAwait(false);
                    if (_disposed) return snapshot;
                    changed = true;
                    _notices.Add(result.Success ? $"Kayıtlı tercih uygulandı · {result.Message}"
                        : $"Kayıtlı koruma uygulanamadı; tekrar denenecek · {result.Message}");
                }
                catch (Exception error)
                {
                    _notices.Add($"Kayıtlı koruma uygulanamadı; tekrar denenecek · {error.Message}");
                }
            }
            if (changed) snapshot = await _inner.RefreshAsync().ConfigureAwait(false);
            var notices = snapshot.Notices.Concat(_notices).ToArray();
            _notices.Clear();
            return snapshot with { Notices = notices };
        }
        finally { _gate.Release(); }
    }

    public async Task<ActionResult> ExecuteAsync(TrainerFeature feature, double value = 0)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return new(false, "Bağlantı kapatıldı.");
            var result = await _inner.ExecuteAsync(feature, value).ConfigureAwait(false);
            if (_disposed || !result.Success || !SavedFeatures.Contains(feature)) return result;
            _desired = feature switch
            {
                TrainerFeature.NoDamage => _desired with { NoDamage = value != 0 },
                TrainerFeature.InfiniteFuel => _desired with { InfiniteFuel = value != 0 },
                _ => _desired with { NoFatigue = value != 0 }
            };
            _retryAfter.Remove(feature);
            try { _store.Save(_desired); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return result with { Message = result.Message + " Tercih dosyaya kaydedilemedi; yeniden açılışta hatırlanmayabilir." };
            }
            return result with { Message = result.Message + " Tercih kaydedildi." };
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Normal shutdown restores native coefficients without changing the saved preference.
        _inner.Dispose();
    }
}
