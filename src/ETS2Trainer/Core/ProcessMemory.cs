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

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

namespace ETS2Trainer.Core;

public sealed class ProcessMemory : IDisposable
{
    private readonly SafeProcessHandle _handle;
    public Process Process { get; }
    public ulong ModuleBase { get; }
    public string ExecutablePath { get; }

    public ProcessMemory(Process process)
    {
        Process = process;
        _handle = OpenProcess(0x438, false, process.Id);
        if (_handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "ETS2 işlemine erişilemiyor.");
        var module = process.MainModule ?? throw new InvalidOperationException("ETS2 modülü okunamadı.");
        ModuleBase = (ulong)module.BaseAddress;
        ExecutablePath = module.FileName;
    }

    public bool Read(ulong address, byte[] buffer, int count) => address >= 0x10000 && ReadProcessMemory(_handle, (nint)address, buffer, (nuint)count, out var read) && read == (nuint)count;
    public T? Read<T>(ulong address) where T : unmanaged
    {
        var b = new byte[Marshal.SizeOf<T>()];
        return Read(address, b, b.Length) ? MemoryMarshal.Read<T>(b) : null;
    }
    public ulong Pointer(ulong address) => Read<ulong>(address) ?? 0;
    public bool IsClass(ulong address, GameClass type) => address >= 0x10000 && type.VtableRva != 0 && Pointer(address) == ModuleBase + type.VtableRva;

    public void Write<T>(ulong address, T value) where T : unmanaged
    {
        var bytes = new byte[Marshal.SizeOf<T>()];
        MemoryMarshal.Write(bytes, in value);
        if (!WriteProcessMemory(_handle, (nint)address, bytes, (nuint)bytes.Length, out var written) || written != (nuint)bytes.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Oyun değeri yazılamadı.");
        if (Read<T>(address) is not { } actual || !EqualityComparer<T>.Default.Equals(actual, value))
            throw new InvalidOperationException("Oyun yazılan değeri doğrulamadı.");
    }

    public List<(ulong Slot, ulong Object)> FindGlobalReferences(GameLayout layout, GameClass type, Func<ulong, bool> validate)
    {
        var matches = new List<(ulong, ulong)>();
        var checkedPointers = new HashSet<ulong>();
        var buffer = new byte[1024 * 1024];
        foreach (var (rva, length) in layout.WritableSections)
        {
            if (length > 64 * 1024 * 1024) continue;
            for (int offset = 0; offset < length; offset += buffer.Length)
            {
                var count = Math.Min(buffer.Length, length - offset);
                var address = ModuleBase + rva + (ulong)offset;
                if (!Read(address, buffer, count)) continue;
                for (int i = 0; i <= count - 8; i += 8)
                {
                    var pointer = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(i, 8));
                    if (pointer < 0x1_0000_0000 || pointer > 0x7fff_ffff_ffff || (pointer & 7) != 0 || pointer >= ModuleBase && pointer < ModuleBase + layout.ImageSize) continue;
                    if (!checkedPointers.Add(pointer) || !IsClass(pointer, type) || !validate(pointer)) continue;
                    matches.Add((address + (ulong)i, pointer));
                }
            }
        }
        return matches;
    }

    public void Dispose() { _handle.Dispose(); Process.Dispose(); }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(SafeProcessHandle handle, nint address, byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(SafeProcessHandle handle, nint address, byte[] buffer, nuint size, out nuint written);
}
