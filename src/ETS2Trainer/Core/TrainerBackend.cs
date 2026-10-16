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

using System.Diagnostics;
using System.IO;

namespace ETS2Trainer.Core;

public sealed class TrainerBackend : ITrainerBackend
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _maintenance;
    private readonly DamagePreventionService _damagePrevention = new();
    private ProcessMemory? _memory;
    private GameLayout? _layout;
    private ulong _economy;
    private ulong _economyReference;
    private DateTime _nextScan;
    private bool _infiniteFuel, _noFatigue, _disposed;
    private string _status = "ETS2 bekleniyor";

    public TrainerBackend() => _maintenance = Task.Run(MaintainAsync);

    public Task<TrainerSnapshot> RefreshAsync() => Task.Run(() =>
    {
        lock (_gate)
        {
            if (_disposed) return new TrainerSnapshot { Status = "Bağlantı kapatıldı" };
            try
            {
                EnsureAttached();
                if (_memory is null || _layout is null) return new TrainerSnapshot { Status = _status };
                if (_economyReference != 0) _economy = _memory.Pointer(_economyReference);
                if (!ValidateEconomy(_economy) && DateTime.UtcNow >= _nextScan)
                {
                    _infiniteFuel = _noFatigue = false;
                    _damagePrevention.Reset();
                    _economy = 0;
                    var candidates = _memory.FindGlobalReferences(_layout, _layout.Economy, ValidateEconomy);
                    _nextScan = DateTime.UtcNow.AddSeconds(8);
                    if (candidates.Count == 1) { _economyReference = candidates[0].Slot; _economy = candidates[0].Object; }
                    _status = candidates.Count > 1 ? "Birden fazla profil nesnesi var; oyun yüklemesini bekleyin" : "Profil yükleyip sürüşe geçin";
                }
                return Snapshot();
            }
            catch (Exception error)
            {
                Disconnect();
                _status = error.Message;
                return new TrainerSnapshot { Status = _status };
            }
        }
    });

    private void EnsureAttached()
    {
        if (_memory is not null && !_memory.Process.HasExited) return;
        Disconnect();
        var processes = Process.GetProcessesByName("eurotrucks2");
        if (processes.Length != 1)
        {
            foreach (var process in processes) process.Dispose();
            _status = processes.Length == 0 ? "ETS2 bekleniyor · Oyunu açıp profilinizi yükleyin" : "Birden fazla ETS2 işlemi açık";
            return;
        }
        _memory = new ProcessMemory(processes[0]);
        _layout = new GameLayout(_memory.ExecutablePath);
        _nextScan = DateTime.MinValue;
        _status = "Oyun bulundu; etkin profil aranıyor";
    }

    private bool ValidateEconomy(ulong address)
    {
        if (_memory is null || _layout is null || !_memory.IsClass(address, _layout.Economy)) return false;
        var bank = _memory.Pointer(address + (ulong)_layout.Economy.Field("bank"));
        var player = _memory.Pointer(address + (ulong)_layout.Economy.Field("player"));
        return _memory.IsClass(bank, _layout.Bank) && _memory.IsClass(player, _layout.Player)
            && _memory.Read<long>(bank + (ulong)_layout.Bank.Field("money_account")) is >= -1_000_000_000 and <= 9_000_000_000_000_000
            && _memory.Read<int>(address + (ulong)_layout.Economy.Field("experience_points")) is >= 0;
    }

    private (ulong Player, ulong Truck, ulong Trailer) ResolveVehicles()
    {
        if (!ValidateEconomy(_economy)) return default;
        var m = _memory!; var l = _layout!;
        var player = m.Pointer(_economy + (ulong)l.Economy.Field("player"));
        ulong truck = 0, trailer = 0;
        if (l.PlayerVehicles is { } vehicles && l.Player.Fields.TryGetValue("assigned_vehicles", out var assigned))
        {
            var active = m.Pointer(player + (ulong)assigned.Offset);
            if (m.IsClass(active, vehicles))
            {
                truck = m.Pointer(active + (ulong)vehicles.Field("vehicle"));
                trailer = m.Pointer(active + (ulong)vehicles.Field("trailer"));
            }
        }
        if (!m.IsClass(truck, l.Vehicle) && l.Player.Fields.TryGetValue("assigned_truck", out var oldTruck))
            truck = m.Pointer(player + (ulong)oldTruck.Offset);
        if (!m.IsClass(truck, l.Vehicle)) truck = 0;
        if (l.Trailer is not { } trailerType || !m.IsClass(trailer, trailerType)) trailer = 0;
        return (player, truck, trailer);
    }

    private TrainerSnapshot Snapshot()
    {
        var m = _memory!; var l = _layout!;
        var profile = ValidateEconomy(_economy);
        var (player, truck, trailer) = ResolveVehicles();
        var features = Enum.GetValues<TrainerFeature>().ToDictionary(f => f, _ => new FeatureStatus(false, "Önce profilinizi yükleyin."));
        if (profile)
        {
            features[TrainerFeature.Money] = features[TrainerFeature.Experience] = new(true);
            features[TrainerFeature.Time] = new(true);
            features[TrainerFeature.NoFatigue] = new(l.Player.Fields.ContainsKey("driving_time"));
        }
        if (truck != 0)
        {
            foreach (var feature in new[] { TrainerFeature.FillFuel, TrainerFeature.InfiniteFuel })
                features[feature] = ReadUnitFloat(truck, l.Vehicle, "fuel_relative") is not null ? new(true) : new(false, "Yakıt alanı doğrulanamadı.");
            foreach (var feature in new[] { TrainerFeature.RepairAll, TrainerFeature.RepairEngine, TrainerFeature.RepairTransmission, TrainerFeature.RepairCabin, TrainerFeature.RepairChassis, TrainerFeature.RepairWheels })
            {
                try { _ = RepairPlan(feature, truck, trailer); features[feature] = new(true); }
                catch (Exception e) { features[feature] = new(false, e.Message); }
            }
            var protection = _damagePrevention.GetStatus(m, l);
            features[TrainerFeature.NoDamage] = _damagePrevention.IsEnabled ? new(true, "Yeni hasarı ve aşınmayı önler. Mevcut hasarı değiştirmez.")
                : protection.Available ? new(true, "Yeni hasarı ve aşınmayı önler. Mevcut hasarı değiştirmez.") : protection;
        }
        else if (profile)
            foreach (var feature in features.Keys.Where(f => f is not (TrainerFeature.Money or TrainerFeature.Experience or TrainerFeature.Time or TrainerFeature.NoFatigue)).ToArray())
                features[feature] = new(false, "Etkin çekici bulunamadı. Bir işe/sürüşe başlayın.");
        if (trailer != 0)
        {
            try { _ = RepairPlan(TrainerFeature.RepairTrailer, truck, trailer); features[TrainerFeature.RepairTrailer] = new(true); }
            catch (Exception e) { features[TrainerFeature.RepairTrailer] = new(false, e.Message); }
        }
        else features[TrainerFeature.RepairTrailer] = new(false, "Bağlı dorse yok.");
        var bank = profile ? m.Pointer(_economy + (ulong)l.Economy.Field("bank")) : 0;
        int? time = profile ? m.Read<int>(_economy + (ulong)l.Economy.Field("game_time")) : null;
        int? experience = profile ? m.Read<int>(_economy + (ulong)l.Economy.Field("experience_points")) : null;
        var cargo = CargoFields(trailer);
        return new TrainerSnapshot
        {
            Connected = profile, Status = profile ? truck != 0 ? "Bağlı · Etkin araç hazır" : "Bağlı · Profil hazır, araç bekleniyor" : _status,
            Version = l.Version, ProcessId = m.Process.Id, Truck = truck != 0 ? "Etkin çekici" : "",
            Money = profile ? m.Read<long>(bank + (ulong)l.Bank.Field("money_account")) : null,
            Experience = experience,
            Level = experience is >= 0 ? RuntimeExperience.Level(m, l, _economy, experience.Value) : null,
            FuelPercent = truck != 0 ? ReadUnitFloat(truck, l.Vehicle, "fuel_relative") * 100 : null,
            EngineDamage = Damage(truck, l.Vehicle, "engine_wear"), TransmissionDamage = Damage(truck, l.Vehicle, "transmission_wear"),
            CabinDamage = Damage(truck, l.Vehicle, "cabin_wear"), ChassisDamage = Damage(truck, l.Vehicle, "chassis_wear"),
            WheelsDamage = ArrayDamage(truck, l.Vehicle), TrailerDamage = trailer != 0 ? Damage(trailer, l.Trailer!, "trailer_body_wear") : null,
            CargoDamage = cargo.Count > 0 ? cargo.Max(field => field.Value) * 100 : null,
            TimeMinutes = time is >= 0 ? time % 1440 : null,
            NoDamage = _damagePrevention.IsEnabled, InfiniteFuel = _infiniteFuel, NoFatigue = _noFatigue, Features = features
        };
    }

    private float? ReadUnitFloat(ulong obj, GameClass type, string field)
    {
        if (obj == 0 || !type.Fields.TryGetValue(field, out var f) || f.Kind != 5) return null;
        var value = _memory!.Read<float>(obj + (ulong)f.Offset);
        return value is >= 0 and <= 1.001f && float.IsFinite(value.Value) ? value : null;
    }
    private float? Damage(ulong obj, GameClass type, string field)
    {
        var value = ReadUnitFloat(obj, type, field);
        var permanent = ReadUnitFloat(obj, type, field + "_unfixable");
        return value is { } v ? Math.Min(100, (v + (permanent ?? 0)) * 100) : null;
    }
    private float? ArrayDamage(ulong obj, GameClass type)
    {
        if (obj == 0) return null;
        try
        {
            var normal = FloatArray(obj, type, "wheels_wear");
            var permanent = FloatArray(obj, type, "wheels_wear_unfixable");
            return Math.Min(100, (normal.Average(v => v.Value) + permanent.Average(v => v.Value)) * 100);
        }
        catch { return null; }
    }

    private List<(ulong Address, float Value)> FloatArray(ulong obj, GameClass type, string name)
    {
        if (!type.Fields.TryGetValue(name, out var field) || field.Kind != 6) throw new InvalidDataException($"{name} desteklenmiyor.");
        var address = obj + (ulong)field.Offset;
        var data = _memory!.Pointer(address + 8);
        var count = _memory.Read<ulong>(address + 16);
        if (data < 0x10000 || count is not (>= 1 and <= 32)) throw new InvalidDataException("Lastik dizisi henüz hazır değil.");
        var result = new List<(ulong, float)>();
        for (ulong i = 0; i < count; i++)
        {
            var value = _memory.Read<float>(data + i * 4);
            if (value is not (>= 0 and <= 1.001f) || !float.IsFinite(value.Value)) throw new InvalidDataException("Lastik hasarı doğrulanamadı.");
            result.Add((data + i * 4, value.Value));
        }
        return result;
    }

    private List<(ulong Address, float Value)> RepairPlan(TrainerFeature feature, ulong truck, ulong trailer)
    {
        var result = new List<(ulong, float)>(); var l = _layout!;
        bool all = feature == TrainerFeature.RepairAll;
        if (truck != 0 && (all || feature != TrainerFeature.RepairTrailer))
        {
            foreach (var (action, field) in new[] { (TrainerFeature.RepairEngine, "engine_wear"), (TrainerFeature.RepairTransmission, "transmission_wear"), (TrainerFeature.RepairCabin, "cabin_wear"), (TrainerFeature.RepairChassis, "chassis_wear") })
                if (all || feature == action) AddWear(truck, l.Vehicle, field, result);
            if (all || feature == TrainerFeature.RepairWheels)
            {
                result.AddRange(FloatArray(truck, l.Vehicle, "wheels_wear"));
                result.AddRange(FloatArray(truck, l.Vehicle, "wheels_wear_unfixable"));
            }
        }
        if (trailer != 0 && (all || feature == TrainerFeature.RepairTrailer))
        {
            AddWear(trailer, l.Trailer!, "trailer_body_wear", result);
            AddWear(trailer, l.Trailer!, "chassis_wear", result);
            result.AddRange(FloatArray(trailer, l.Trailer!, "wheels_wear"));
            result.AddRange(FloatArray(trailer, l.Trailer!, "wheels_wear_unfixable"));
        }
        if (feature == TrainerFeature.RepairAll) result.AddRange(CargoFields(trailer));
        if (result.Count == 0) throw new InvalidOperationException("Onarılacak etkin araç bulunamadı.");
        return result;
    }

    private List<(ulong Address, float Value)> CargoFields(ulong trailer)
    {
        var result = new List<(ulong, float)>();
        var m = _memory; var l = _layout;
        if (m is null || l?.Trailer is not { } type || l.PlayerJob is not { } jobType || trailer == 0
            || !l.Player.Fields.TryGetValue("current_job", out var currentJob) || currentJob.Kind != 61
            || !jobType.Fields.TryGetValue("is_trailer_loaded", out var loaded) || loaded.Kind != 57
            || !jobType.Fields.TryGetValue("company_trailer", out var company) || company.Kind != 61
            || !type.Fields.TryGetValue("cargo_damage", out var damage) || damage.Kind != 5)
            return result;
        var player = m.Pointer(_economy + (ulong)l.Economy.Field("player"));
        var job = m.Pointer(player + (ulong)currentJob.Offset);
        if (!m.IsClass(job, jobType) || m.Read<byte>(job + (ulong)loaded.Offset) != 1
            || m.Pointer(job + (ulong)company.Offset) != trailer) return result;
        var seen = new HashSet<ulong>();
        while (trailer != 0 && seen.Add(trailer) && seen.Count <= 8)
        {
            if (!m.IsClass(trailer, type)) return [];
            var value = ReadUnitFloat(trailer, type, "cargo_damage");
            if (value is not { } verified) return [];
            result.Add((trailer + (ulong)damage.Offset, verified));
            trailer = type.Fields.TryGetValue("slave_trailer", out var slave) && slave.Kind == 61
                ? m.Pointer(trailer + (ulong)slave.Offset) : 0;
        }
        return trailer == 0 ? result : [];
    }
    private void AddWear(ulong obj, GameClass type, string name, List<(ulong, float)> output)
    {
        foreach (var field in new[] { name, name + "_unfixable" })
        {
            var value = ReadUnitFloat(obj, type, field) ?? throw new InvalidDataException($"{field} doğrulanamadı.");
            output.Add((obj + (ulong)type.Field(field), value));
        }
    }

    public Task<ActionResult> ExecuteAsync(TrainerFeature feature, double value = 0) => Task.Run(() =>
    {
        lock (_gate)
        {
            try
            {
                if (feature == TrainerFeature.NoDamage && value == 0) return _damagePrevention.Reset();
                if (!ValidateEconomy(_economy)) throw new InvalidOperationException("Önce oyunda profil yükleyin.");
                var snapshot = Snapshot();
                if (!snapshot.Features.TryGetValue(feature, out var available) || !available.Available)
                    throw new InvalidOperationException(available?.Reason ?? "Bu özellik hazır değil.");
                if (!double.IsFinite(value)) throw new ArgumentException("Geçerli bir sayı girin.");
                var m = _memory!; var l = _layout!;
                var (player, truck, trailer) = ResolveVehicles();
                switch (feature)
                {
                    case TrainerFeature.Money:
                        if (value < 0 || value > 9_000_000_000_000_000 || value != Math.Truncate(value)) throw new ArgumentOutOfRangeException(nameof(value), "Para tam sayı ve 0–9.000.000.000.000.000 aralığında olmalı.");
                        var bank = m.Pointer(_economy + (ulong)l.Economy.Field("bank"));
                        m.Write(bank + (ulong)l.Bank.Field("money_account"), checked((long)value));
                        return Success($"Para güncellendi: € {value:N0}");
                    case TrainerFeature.Experience:
                        if (value < 0 || value > int.MaxValue || value != Math.Truncate(value)) throw new ArgumentOutOfRangeException(nameof(value), "XP 0–2.147.483.647 aralığında olmalı.");
                        m.Write(_economy + (ulong)l.Economy.Field("experience_points"), checked((int)value));
                        return Success($"XP güncellendi: {value:N0}");
                    case TrainerFeature.Time:
                        if (value < 0 || value > 1439 || value != Math.Truncate(value)) throw new ArgumentOutOfRangeException(nameof(value));
                        var timeAddress = _economy + (ulong)l.Economy.Field("game_time");
                        var current = m.Read<int>(timeAddress) ?? throw new InvalidOperationException("Saat okunamadı.");
                        int next = checked(current / 1440 * 1440 + (int)value);
                        if (next < current) next = checked(next + 1440);
                        m.Write(timeAddress, next);
                        return Success($"Oyun saati {((int)value / 60):00}:{((int)value % 60):00} olarak ayarlandı.");
                    case TrainerFeature.FillFuel:
                        m.Write(truck + (ulong)l.Vehicle.Field("fuel_relative"), 1f);
                        return Success("Yakıt deposu dolduruldu.");
                    case TrainerFeature.NoDamage:
                        return _damagePrevention.SetEnabled(true, m, l);
                    case TrainerFeature.InfiniteFuel:
                        if (value != 0) m.Write(truck + (ulong)l.Vehicle.Field("fuel_relative"), 1f);
                        _infiniteFuel = value != 0;
                        return Success(_infiniteFuel ? "Sınırsız yakıt açık." : "Sınırsız yakıt kapalı.");
                    case TrainerFeature.NoFatigue:
                        if (value != 0) m.Write(player + (ulong)l.Player.Field("driving_time"), 0);
                        _noFatigue = value != 0;
                        return Success(_noFatigue ? "Yorgunluk sıfırlama açık." : "Yorgunluk sıfırlama kapalı.");
                    default:
                        var hasCargo = feature == TrainerFeature.RepairAll && CargoFields(trailer).Count > 0;
                        ApplyRepairs(RepairPlan(feature, truck, trailer));
                        return Success(feature == TrainerFeature.RepairAll
                            ? hasCargo ? "Çekici, bağlı dorse ve yük onarıldı." : trailer != 0 ? "Çekici ve bağlı dorse onarıldı. Doğrulanmış yüklü dorse bulunamadı." : "Çekici tamamen onarıldı."
                            : "Seçilen parça onarıldı.");
                }
            }
            catch (Exception e) { return new ActionResult(false, e.Message); }
        }
    });

    private void ApplyRepairs(List<(ulong Address, float Value)> plan)
    {
        var applied = new List<(ulong Address, float Value)>();
        try
        {
            foreach (var item in plan.DistinctBy(i => i.Address))
            {
                _memory!.Write(item.Address, 0f); applied.Add(item);
            }
        }
        catch
        {
            foreach (var item in applied) { try { _memory!.Write(item.Address, item.Value); } catch { } }
            throw;
        }
    }

    private async Task MaintainAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await Task.Delay(100, _stop.Token);
                lock (_gate)
                {
                    if (_disposed || (!_damagePrevention.IsEnabled && !_infiniteFuel && !_noFatigue)) continue;
                    try
                    {
                        if (_memory is null || _memory.Process.HasExited || !ValidateEconomy(_economy))
                        {
                            _infiniteFuel = _noFatigue = false;
                            _damagePrevention.Reset();
                            continue;
                        }
                        if (_damagePrevention.IsEnabled && !_damagePrevention.ValidateConnection(_memory, _layout!)) _damagePrevention.Reset();
                        if (!_infiniteFuel && !_noFatigue) continue;
                        var (player, truck, trailer) = ResolveVehicles();
                        if (_infiniteFuel && truck != 0 && ReadUnitFloat(truck, _layout!.Vehicle, "fuel_relative") is not null)
                            _memory!.Write(truck + (ulong)_layout!.Vehicle.Field("fuel_relative"), 1f);
                        if (_noFatigue) _memory!.Write(player + (ulong)_layout!.Player.Field("driving_time"), 0);
                    }
                    catch { _infiniteFuel = _noFatigue = false; _damagePrevention.Reset(); }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static ActionResult Success(string message) => new(true, message);
    private void Disconnect()
    {
        _infiniteFuel = _noFatigue = false;
        _damagePrevention.Reset();
        _economy = _economyReference = 0; _layout = null; _memory?.Dispose(); _memory = null;
    }
    public void Dispose()
    {
        _stop.Cancel();
        lock (_gate) { _disposed = true; Disconnect(); _damagePrevention.Dispose(); }
        _stop.Dispose();
    }
}
