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

public readonly record struct GameProcessIdentity(int ProcessId, long StartTimeUtcTicks);

public sealed class GameLaunchTracker
{
    private HashSet<GameProcessIdentity> _observed = [];

    public bool Observe(IEnumerable<GameProcessIdentity> games)
    {
        var current = games.ToHashSet();
        var newLaunch = current.Any(game => !_observed.Contains(game));
        _observed = current;
        return newLaunch;
    }
}
