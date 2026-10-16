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

namespace ETS2Trainer.Core;

public enum TrainerFeature
{
    RepairAll, RepairEngine, RepairTransmission, RepairCabin, RepairChassis, RepairWheels,
    RepairTrailer, NoDamage, FillFuel, InfiniteFuel, Money, Experience, Time, NoFatigue
}

public sealed record FeatureStatus(bool Available, string Reason = "");

public sealed record TrainerSnapshot
{
    public bool Connected { get; init; }
    public string Status { get; init; } = "ETS2 bekleniyor";
    public string Version { get; init; } = "";
    public int? ProcessId { get; init; }
    public string Truck { get; init; } = "";
    public long? Money { get; init; }
    public int? Experience { get; init; }
    public int? Level { get; init; }
    public float? FuelPercent { get; init; }
    public float? EngineDamage { get; init; }
    public float? TransmissionDamage { get; init; }
    public float? CabinDamage { get; init; }
    public float? ChassisDamage { get; init; }
    public float? WheelsDamage { get; init; }
    public float? TrailerDamage { get; init; }
    public float? CargoDamage { get; init; }
    public int? TimeMinutes { get; init; }
    public bool NoDamage { get; init; }
    public bool InfiniteFuel { get; init; }
    public bool NoFatigue { get; init; }
    public IReadOnlyList<string> Notices { get; init; } = [];
    public IReadOnlyDictionary<TrainerFeature, FeatureStatus> Features { get; init; } = new Dictionary<TrainerFeature, FeatureStatus>();
}

public sealed record ActionResult(bool Success, string Message);

public interface ITrainerBackend : IDisposable
{
    Task<TrainerSnapshot> RefreshAsync();
    Task<ActionResult> ExecuteAsync(TrainerFeature feature, double value = 0);
}
