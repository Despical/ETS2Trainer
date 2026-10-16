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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ETS2Trainer.Core;

// Prism3D ships the names, types and member offsets used by its SII serializer.
// Resolve those declarations from this exact executable instead of reusing old offsets.
public sealed class GameLayout
{
    private readonly byte[] _image;
    private readonly List<Section> _sections = [];
    private readonly Dictionary<ulong, GameClass> _classes = [];
    public ulong PreferredBase { get; }
    public uint ImageSize { get; }
    public IReadOnlyList<(uint Rva, int Length)> WritableSections => _sections.Where(s => (s.Flags & 0x80000000) != 0).Select(s => (s.Rva, s.VirtualLength)).ToArray();
    public string Version { get; }
    public string Sha256 { get; }
    public GameClass Economy { get; }
    public GameClass Bank { get; }
    public GameClass Player { get; }
    public GameClass Vehicle { get; }
    public GameClass? PlayerVehicles { get; }
    public GameClass? Trailer { get; }
    public GameClass? PlayerJob { get; }
    public IReadOnlyCollection<GameClass> Classes => _classes.Values;

    public GameLayout(string path)
    {
        _image = File.ReadAllBytes(path);
        if (_image.Length is < 4096 or > 200_000_000)
            throw new InvalidDataException("Oyun dosyası boyutu desteklenmiyor.");
        var pe = I32(0x3c);
        if (U32(pe) != 0x4550 || U16(pe + 24) != 0x20b)
            throw new InvalidDataException("64 bit ETS2 yürütülebilir dosyası gerekli.");
        PreferredBase = U64(pe + 48);
        ImageSize = U32(pe + 80);
        Version = FileVersionInfo.GetVersionInfo(path).FileVersion?.Split(' ')[0] ?? "Bilinmiyor";
        Sha256 = Convert.ToHexString(SHA256.HashData(_image)).ToLowerInvariant();
        int count = U16(pe + 6), table = pe + 24 + U16(pe + 20);
        for (var i = 0; i < count; i++)
        {
            int p = table + i * 40;
            var section = new Section(U32(p + 12), checked((int)U32(p + 20)), checked((int)U32(p + 16)), checked((int)Math.Max(U32(p + 8), U32(p + 16))), U32(p + 36));
            if (section.Offset >= 0 && section.Length > 0 && section.Offset + (long)section.Length <= _image.Length)
                _sections.Add(section);
        }

        string[] seeds = ["experience_points", "money_account", "fuel_relative", "assigned_vehicles", "assigned_truck", "trailer_body_wear", "is_trailer_loaded", "level_xp", "reward_bonus_level"];
        foreach (var seed in seeds)
            DiscoverOwners(seed);
        Economy = Require("economy", "experience_points");
        Bank = Require("bank", "money_account");
        Player = Require("player", "assigned_truck");
        Vehicle = Require("vehicle", "fuel_relative");
        PlayerVehicles = _classes.Values.SingleOrDefault(c => c.Name == "player_vehicles");
        if (PlayerVehicles is null)
        {
            DiscoverOwners("vehicle");
            PlayerVehicles = _classes.Values.SingleOrDefault(c => c.Name == "player_vehicles");
        }
        Trailer = _classes.Values.SingleOrDefault(c => c.Name == "trailer" && c.Fields.ContainsKey("trailer_body_wear"));
        PlayerJob = _classes.Values.SingleOrDefault(c => c.Name == "player_job" && c.Fields.ContainsKey("is_trailer_loaded"));
        foreach (var type in _classes.Values)
            type.VtableRva = FindVtable(type.Address);
        foreach (var type in new[] { Economy, Bank, Player, Vehicle })
            if (type.VtableRva == 0)
                throw new InvalidDataException($"{type.Name}: oyun nesnesi kimliği doğrulanamadı.");
    }

    private GameClass Require(string name, string field) => _classes.Values.SingleOrDefault(c => c.Name == name && c.Fields.ContainsKey(field))
        ?? throw new InvalidDataException($"Bu sürümde {name}.{field} alanı bulunamadı.");

    private void DiscoverOwners(string field)
    {
        var needle = Encoding.ASCII.GetBytes(field + '\0');
        foreach (int stringOffset in Find(needle))
        {
            if (stringOffset > 0 && _image[stringOffset - 1] != 0)
                continue;
            var address = ToVa(stringOffset);
            if (address == 0) continue;
            foreach (var pointerOffset in Find(BitConverter.GetBytes(address)))
            {
                if (pointerOffset < 32) continue;
                var owner = U64(pointerOffset - 32);
                var memberOffset = U64(pointerOffset - 24);
                var kind = U64(pointerOffset - 16);
                if (memberOffset > 0x10000 || kind > 0x40 || Offset(owner) < 0) continue;
                TryClass(owner);
            }
        }
    }

    private GameClass? TryClass(ulong address)
    {
        if (_classes.TryGetValue(address, out var cached)) return cached;
        var p = Offset(address);
        if (p < 0 || p + 72 > _image.Length) return null;
        var nameTable = Offset(U64(p));
        if (nameTable < 0) return null;
        var name = ReadString(U64(nameTable));
        var size = U64(p + 48);
        var start = Offset(U64(p + 32));
        if (name.Length is < 2 or > 80 || size is < 16 or > 0x10000 || start < 0) return null;
        var fields = new Dictionary<string, GameField>(StringComparer.Ordinal);
        for (var i = 0; i < 512; i++)
        {
            var q = start + i * 40;
            if (q + 40 > _image.Length || U64(q) != address) break;
            var fieldName = ReadString(U64(q + 32));
            var fieldOffset = U64(q + 8);
            var kind = U64(q + 16);
            if (fieldName.Length > 0 && fieldOffset < size && kind <= 0x40)
                fields.TryAdd(fieldName, new GameField(checked((int)fieldOffset), (int)kind));
        }
        if (fields.Count == 0) return null;
        var result = new GameClass(name, address, (int)size, fields);
        _classes.Add(address, result);
        var parent = TryClass(U64(p + 24));
        if (parent is not null)
            foreach (var f in parent.Fields) fields.TryAdd(f.Key, f.Value);
        return result;
    }

    private ulong FindVtable(ulong descriptor)
    {
        foreach (var section in _sections.Where(s => (s.Flags & 0x20000000) != 0))
        {
            var end = section.Offset + section.Length - 8;
            for (var i = section.Offset; i <= end; i++)
            {
                if (_image[i] != 0x48 || _image[i + 1] != 0x8d || _image[i + 2] != 0x05 || _image[i + 7] != 0xc3) continue;
                var instruction = PreferredBase + section.Rva + (ulong)(i - section.Offset);
                var dest = (ulong)((long)instruction + 7 + I32(i + 3));
                if (dest != descriptor) continue;
                var candidates = Find(BitConverter.GetBytes(instruction))
                    .Select(o => ToVa(o) - 40)
                    .Where(v => v >= PreferredBase && IsCodePointer(v) && IsCodePointer(v + 8))
                    .Distinct().ToArray();
                if (candidates.Length == 1) return candidates[0] - PreferredBase;
            }
        }
        return 0;
    }

    private bool IsCodePointer(ulong at)
    {
        var p = Offset(at);
        if (p < 0 || p + 8 > _image.Length) return false;
        var v = U64(p);
        return v >= PreferredBase && _sections.Any(s => (s.Flags & 0x20000000) != 0 && v - PreferredBase >= s.Rva && v - PreferredBase < s.Rva + (ulong)s.Length);
    }

    private IEnumerable<int> Find(byte[] needle)
    {
        int from = 0;
        while (from <= _image.Length - needle.Length)
        {
            var index = _image.AsSpan(from).IndexOf(needle);
            if (index < 0) yield break;
            var result = from + index;
            yield return result;
            from = result + 1;
        }
    }

    private string ReadString(ulong va)
    {
        int p = Offset(va);
        if (p < 0) return "";
        int length = 0;
        while (p + length < _image.Length && length < 120 && _image[p + length] != 0)
        {
            if (_image[p + length] is < 32 or > 126) return "";
            length++;
        }
        return length >= 120 ? "" : Encoding.ASCII.GetString(_image, p, length);
    }

    private int Offset(ulong va)
    {
        if (va < PreferredBase) return -1;
        var rva = va - PreferredBase;
        foreach (var s in _sections)
            if (rva >= s.Rva && rva < s.Rva + (ulong)s.Length)
                return s.Offset + (int)(rva - s.Rva);
        return -1;
    }

    private ulong ToVa(int offset)
    {
        foreach (var s in _sections)
            if (offset >= s.Offset && offset < s.Offset + s.Length)
                return PreferredBase + s.Rva + (ulong)(offset - s.Offset);
        return 0;
    }
    private ushort U16(int p) => BinaryPrimitives.ReadUInt16LittleEndian(_image.AsSpan(p, 2));
    private uint U32(int p) => BinaryPrimitives.ReadUInt32LittleEndian(_image.AsSpan(p, 4));
    private int I32(int p) => BinaryPrimitives.ReadInt32LittleEndian(_image.AsSpan(p, 4));
    private ulong U64(int p) => BinaryPrimitives.ReadUInt64LittleEndian(_image.AsSpan(p, 8));
    private sealed record Section(uint Rva, int Offset, int Length, int VirtualLength, uint Flags);
}

public sealed record GameField(int Offset, int Kind);
public sealed record GameClass(string Name, ulong Address, int Size, Dictionary<string, GameField> Fields)
{
    public ulong VtableRva { get; set; }
    public int Field(string name) => Fields.TryGetValue(name, out var f) ? f.Offset : throw new InvalidDataException($"{Name}.{name} bulunamadı.");
}
