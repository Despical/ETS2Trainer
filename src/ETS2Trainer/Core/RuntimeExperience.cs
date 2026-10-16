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

using System.Buffers.Binary;

namespace ETS2Trainer.Core;

public static class RuntimeExperience
{
    // These two nonserialized links are admitted only for this measured build.
    // 0x140498222 loads economy+170, XP, config+138, then calls the XP lookup.
    private const string SupportedImage = "b70c2357cc56eb5834ee47fea3c60b2f2d4591383ff2ac479cfd3cd254301b8c";

    public static int? Level(ProcessMemory memory, GameLayout layout, ulong economy, int experience)
    {
        if (experience < 0 || layout.Sha256 != SupportedImage || !memory.IsClass(economy, layout.Economy)) return null;
        var configType = layout.Classes.SingleOrDefault(c => c.Name == "economy_data");
        var xpType = layout.Classes.SingleOrDefault(c => c.Name == "xp_level_data");
        if (configType is null || xpType is null || !xpType.Fields.TryGetValue("level_xp", out var field)) return null;
        var config = memory.Pointer(economy + 0x170);
        if (!memory.IsClass(config, configType)) return null;
        var xp = memory.Pointer(config + 0x138);
        if (!memory.IsClass(xp, xpType)) return null;
        var data = memory.Pointer(xp + (ulong)field.Offset + 8);
        var count = memory.Read<ulong>(xp + (ulong)field.Offset + 16);
        if (data < 0x10000 || count is not (>= 1 and <= 1024)) return null;
        var bytes = new byte[checked((int)count * 4)];
        if (!memory.Read(data, bytes, bytes.Length)) return null;
        var increments = new int[(int)count];
        for (var i = 0; i < increments.Length; i++)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
            if (value is 0 or > int.MaxValue) return null;
            increments[i] = (int)value;
        }
        return new ExperienceProgression(increments).At(experience).Level;
    }
}
