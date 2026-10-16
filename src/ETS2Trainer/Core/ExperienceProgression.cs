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

namespace ETS2Trainer.Core;

public sealed class ExperienceProgression
{
    // Stock ETS2 1.61.1.9: /def/economy_data_xp.sii. The native XP lookup
    // repeats the final increment after the last configured table entry.
    public static ExperienceProgression Stock { get; } = new([
        200, 500, 700, 900, 1000, 1100, 1300, 1600, 1700, 2100,
        2300, 2600, 2700, 2900, 3000, 3100, 3400, 3700, 4000, 4300,
        4600, 4700, 4900, 5200, 5700, 5900, 6000, 6200, 6600, 6800
    ]);

    private readonly int[] _increments;

    public ExperienceProgression(IEnumerable<int> increments)
    {
        ArgumentNullException.ThrowIfNull(increments);
        _increments = increments.ToArray();
        if (_increments.Length == 0 || _increments.Any(x => x <= 0))
            throw new ArgumentException("XP artışları pozitif olmalı.", nameof(increments));
    }

    public ExperienceProgress At(int experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        int level = 0, remaining = experience;
        while (level < _increments.Length && remaining >= _increments[level])
        {
            remaining -= _increments[level];
            level++;
        }
        if (level == _increments.Length)
        {
            level += remaining / _increments[^1];
            remaining %= _increments[^1];
        }
        int required = _increments[Math.Min(level, _increments.Length - 1)];
        long startsAt = (long)experience - remaining;
        return new ExperienceProgress(level, remaining, required, startsAt, startsAt + required);
    }
}

public sealed record ExperienceProgress(int Level, int ExperienceInLevel, int RequiredForNextLevel,
    long LevelStartsAt, long NextLevelAt);
