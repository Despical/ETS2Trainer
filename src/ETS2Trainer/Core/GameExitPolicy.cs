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

public sealed class GameExitPolicy
{
    private bool _gameObserved;
    private bool _exitRequested;

    public void Reset()
    {
        _gameObserved = false;
        _exitRequested = false;
    }

    public bool Observe(bool enabled, IReadOnlyCollection<GameProcessIdentity>? games)
    {
        if (!enabled)
        {
            Reset();
            return false;
        }
        if (games is null || _exitRequested) return false;
        if (games.Count > 0)
        {
            _gameObserved = true;
            return false;
        }
        if (!_gameObserved) return false;
        _exitRequested = true;
        return true;
    }
}
