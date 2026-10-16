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

using System.Buffers.Binary;
using System.IO;
using System.Reflection.PortableExecutable;

namespace ETS2Trainer.Core;

// Modify the damage calculation's source coefficients, not the truck's damage.
// The game's own impact/wear routines read this typed unit on every calculation.
public sealed class DamagePreventionService : IDisposable
{
    internal static readonly string[] ProtectedFields =
    [
        "side_damage_factor", "roof_damage_factor", "truck_damage_coef", "trailer_damage_coef",
        "dragged_trailer_damage_coef", "truck_to_trailer_dmg", "car_to_cargo_dmg",
        "cabin_damage_ratio", "chassis_damage_ratio", "engine_damage_ratio", "transmission_damage_ratio", "wheel_damage_ratio",
        "cabin_unfixable_damage_ratio", "chassis_unfixable_damage_ratio", "engine_unfixable_damage_ratio", "transmission_unfixable_damage_ratio", "wheel_unfixable_damage_ratio",
        "trailer_body_damage_ratio", "trailer_chassis_damage_ratio", "trailer_wheel_damage_ratio",
        "trailer_body_unfixable_damage_ratio", "trailer_chassis_unfixable_damage_ratio", "trailer_wheel_unfixable_damage_ratio", "cargo_damage_ratio",
        "cabin_wear", "chassis_wear", "engine_wear", "transmission_wear", "wheel_wear",
        "cabin_wear_unfixable", "chassis_wear_unfixable", "engine_wear_unfixable", "transmission_wear_unfixable", "wheel_wear_unfixable",
        "trailer_body_wear", "trailer_chassis_wear", "trailer_wheel_wear",
        "trailer_body_wear_unfixable", "trailer_chassis_wear_unfixable", "trailer_wheel_wear_unfixable", "odometer_wear_unfixable"
    ];
    private string? _signatureSha;
    private Root? _root;
    private string _signatureError = "";
    private Session? _session;
    private bool _disposed;
    public bool IsEnabled { get; private set; }

    public FeatureStatus GetStatus(ProcessMemory memory, GameLayout layout)
    {
        try { return GetStatus(CreateAccess(memory, layout)); }
        catch (Exception error) { return new(false, error.Message); }
    }

    public ActionResult SetEnabled(bool enabled, ProcessMemory memory, GameLayout layout)
    {
        if (!enabled) return Reset();
        try { return SetEnabled(true, CreateAccess(memory, layout)); }
        catch (Exception error) { return new(false, error.Message); }
    }

    // Lifecycle validation only: it never repairs damage or rewrites coefficients.
    public bool ValidateConnection(ProcessMemory memory, GameLayout layout)
    {
        if (!IsEnabled) return true;
        try { return ValidateConnection(CreateAccess(memory, layout)); }
        catch { return false; }
    }

    internal FeatureStatus GetStatus(Access access)
    {
        if (_disposed) return new(false, "Hasar koruması kapatıldı.");
        try { _ = Capture(access); return new(true); }
        catch (Exception error) { return new(false, error.Message); }
    }

    internal ActionResult SetEnabled(bool enabled, Access access)
    {
        if (!enabled) return Reset();
        if (_disposed) return new(false, "Hasar koruması kapatıldı.");
        if (IsEnabled)
            return ValidateConnection(access) ? new(true, "Doğrudan hasar koruması zaten açık.") : new(false, "Oyun hasar verisi değişti. Korumayı kapatıp tekrar açın.");
        if (_session is not null)
        {
            var reset = Reset();
            if (!reset.Success) return reset;
        }
        Session? session = null;
        try
        {
            session = Capture(access);
            _session = session;
            foreach (var field in session.Fields)
            {
                if (!SameObject(session)) throw new InvalidDataException("Oyun hasar verisi işlem sırasında değişti.");
                if (field.Original == 0) continue;
                // Mark before calling: a failed readback may follow a completed write.
                field.Applied = true;
                access.Write(session.Object + (ulong)field.Offset, 0f);
                if (access.Read(session.Object + (ulong)field.Offset) != 0f)
                    throw new IOException("Hasar katsayısı yazımı doğrulanamadı.");
            }
            if (!ValidateConnection(access, session)) throw new IOException("Hasar koruması doğrulanamadı.");
            IsEnabled = true;
            return new(true, "Hasar oluşumu ve kilometre aşınması kapalı. Mevcut hasar korunur.");
        }
        catch (Exception error)
        {
            var restore = Reset();
            return new(false, restore.Success ? error.Message : error.Message + " " + restore.Message);
        }
    }

    internal bool ValidateConnection(Access access) => _session is { } session
        && ReferenceEquals(session.Access.Identity, access.Identity) && ValidateConnection(access, session);

    private static bool ValidateConnection(Access access, Session session) => SameObject(session)
        && access.Resolve() == session.Object && access.IsType(session.Object)
        && session.Fields.All(field => access.Read(session.Object + (ulong)field.Offset) == 0f);

    private static Session Capture(Access access)
    {
        var address = access.Resolve();
        if (!access.IsType(address)) throw new InvalidDataException("Etkin hasar hesaplama verisi doğrulanamadı. Profilinizi yükleyin.");
        var fields = new List<OwnedField>();
        foreach (var name in ProtectedFields)
        {
            if (!access.Fields.TryGetValue(name, out var field) || field.Kind != 5)
                throw new InvalidDataException($"Bu sürümde hasar katsayısı bulunamadı: {name}.");
            var value = access.Read(address + (ulong)field.Offset);
            if (value is not >= 0 or > 1_000_000 || !float.IsFinite(value.Value))
                throw new InvalidDataException($"Hasar katsayısı doğrulanamadı: {name}.");
            fields.Add(new(field.Offset, value.Value));
        }
        if (access.Resolve() != address || !access.IsType(address))
            throw new InvalidDataException("Oyun hasar verisi yenileniyor. Tekrar deneyin.");
        return new(access, address, fields);
    }

    // Called before ProcessMemory is disposed. Restore only our still-zero values
    // on the same active typed object; never overwrite another tool's changes.
    public ActionResult Reset()
    {
        IsEnabled = false;
        if (_session is not { } session) return new(true, "Doğrudan hasar koruması kapalı.");
        try
        {
            if (!SameObject(session))
            {
                _session = null;
                return new(false, "Oyun hasar verisi değişti; önceki nesneye yazılmadı.");
            }
            var changedElsewhere = 0;
            foreach (var field in session.Fields.Where(field => field.Applied))
            {
                if (!SameObject(session)) throw new InvalidDataException("Hasar verisi geri yükleme sırasında değişti.");
                var address = session.Object + (ulong)field.Offset;
                var value = session.Access.Read(address);
                if (value is null) throw new IOException("Özgün hasar katsayıları okunamadı.");
                if (value == field.Original) { field.Applied = false; continue; }
                if (value != 0f) { field.Applied = false; changedElsewhere++; continue; }
                session.Access.Write(address, field.Original);
                if (session.Access.Read(address) != field.Original)
                    throw new IOException("Özgün hasar katsayısı geri yüklenemedi.");
                field.Applied = false;
            }
            _session = null;
            return changedElsewhere == 0
                ? new(true, "Hasar koruması kapalı; özgün hasar ve aşınma katsayıları geri yüklendi.")
                : new(false, $"Koruma kapalı; başka bir işlem tarafından değiştirilen {changedElsewhere} katsayı korunarak diğerleri geri yüklendi.");
        }
        catch (Exception error) { return new(false, error.Message); }
    }

    private static bool SameObject(Session session) => session.Access.Resolve() == session.Object && session.Access.IsType(session.Object);

    private Access CreateAccess(ProcessMemory memory, GameLayout layout)
    {
        var type = layout.Classes.SingleOrDefault(type => type.Name == "vehicle_damage_data" && type.Fields.ContainsKey("truck_damage_coef"))
            ?? throw new InvalidDataException("Bu sürümde doğrudan hasar koruması bulunamadı.");
        if (_signatureSha != layout.Sha256)
        {
            _signatureSha = layout.Sha256; _root = null; _signatureError = "";
            try { _root = ResolveRoot(memory.ExecutablePath, layout); }
            catch (Exception error) { _signatureError = error.Message; }
        }
        var root = _root ?? throw new InvalidDataException(_signatureError);
        return new(memory, () => memory.Pointer(memory.Pointer(memory.ModuleBase + root.SlotRva) + (ulong)root.MemberOffset),
            address => memory.IsClass(address, type), type.Fields,
            address => memory.Read<float>(address), (address, value) => memory.Write(address, value));
    }

    private static Root ResolveRoot(string path, GameLayout layout)
    {
        // Verified consumer: load the controller, load its damage unit, multiply
        // by engine_damage_ratio, then add that result to vehicle.engine_wear.
        // Resolve the two pointer operands from this exact build's instruction bytes.
        using var file = File.OpenRead(path);
        using var pe = new PEReader(file, PEStreamOptions.LeaveOpen);
        var candidates = new HashSet<Root>();
        var buffer = new byte[65536 + 96];
        foreach (var section in pe.PEHeaders.SectionHeaders.Where(section => (section.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0))
        {
            file.Position = section.PointerToRawData;
            var remaining = section.SizeOfRawData;
            var processed = 0; var carry = 0;
            while (remaining > 0)
            {
                var count = Math.Min(65536, remaining);
                file.ReadExactly(buffer.AsSpan(carry, count));
                var length = carry + count;
                for (var i = 0; i <= length - 96; i++)
                {
                    var code = buffer.AsSpan(i, 96);
                    if (!MatchesConsumer(code, layout)) continue;
                    var instructionRva = section.VirtualAddress + processed - carry + i;
                    var slotRva = (long)instructionRva + 7 + BinaryPrimitives.ReadInt32LittleEndian(code[3..]);
                    var member = BinaryPrimitives.ReadInt32LittleEndian(code[17..]);
                    if (slotRva < 0 || member is < 16 or > 65536 || (member & 7) != 0) continue;
                    if (!layout.WritableSections.Any(section => (ulong)slotRva >= section.Rva && (ulong)slotRva + 8 <= section.Rva + (ulong)section.Length)) continue;
                    candidates.Add(new((ulong)slotRva, member));
                }
                carry = Math.Min(95, length);
                buffer.AsSpan(length - carry, carry).CopyTo(buffer);
                processed += count; remaining -= count;
            }
        }
        return candidates.Count == 1 ? candidates.Single()
            : throw new InvalidDataException("Bu oyun sürümünde hasar hesaplama yolu güvenilir biçimde bulunamadı.");
    }

    private static bool MatchesConsumer(ReadOnlySpan<byte> code, GameLayout layout)
    {
        if (code[0] != 0x48 || code[1] != 0x8b || code[2] != 0x05) return false;
        ReadOnlySpan<byte> load = [0x4c, 0x8b, 0xd1, 0x41, 0x8b, 0x50, 0x48, 0x48, 0x8b, 0x80];
        ReadOnlySpan<byte> multiply = [0xf3, 0x0f, 0x59, 0x40];
        ReadOnlySpan<byte> add = [0xf3, 0x41, 0x0f, 0x58, 0x80];
        if (!code.Slice(7, 10).SequenceEqual(load) || !code.Slice(0x47, 4).SequenceEqual(multiply)
            || !code.Slice(0x50, 5).SequenceEqual(add)) return false;
        var type = layout.Classes.SingleOrDefault(type => type.Name == "vehicle_damage_data" && type.Fields.ContainsKey("truck_damage_coef"));
        return type is not null && code[0x4b] == type.Field("engine_damage_ratio")
            && BinaryPrimitives.ReadInt32LittleEndian(code[0x55..]) == layout.Vehicle.Field("engine_wear");
    }

    public void Dispose() { if (_disposed) return; _ = Reset(); _disposed = true; }
    internal sealed record Access(object Identity, Func<ulong> Resolve, Func<ulong, bool> IsType, Dictionary<string, GameField> Fields,
        Func<ulong, float?> Read, Action<ulong, float> Write);
    private sealed record Root(ulong SlotRva, int MemberOffset);
    private sealed record Session(Access Access, ulong Object, List<OwnedField> Fields);
    private sealed class OwnedField(int offset, float original)
    {
        public int Offset { get; } = offset;
        public float Original { get; } = original;
        public bool Applied { get; set; }
    }
}
