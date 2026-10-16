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

using System.Security.Principal;

namespace ETS2Trainer.Core;

internal sealed class TrainerInstance : IDisposable
{
    private static readonly string UserId = WindowsIdentity.GetCurrent().User!.Value;
    private readonly Mutex _mutex;
    private bool _owned;

    internal static string Name(string role) => $@"Local\ETS2Trainer.{UserId}.{role}";

    private TrainerInstance(Mutex mutex) => _mutex = mutex;

    internal static TrainerInstance? TryAcquire(string role)
    {
        var instance = new TrainerInstance(new Mutex(false, Name(role)));
        try { instance._owned = instance._mutex.WaitOne(0); }
        catch (AbandonedMutexException) { instance._owned = true; }
        if (instance._owned) return instance;
        instance.Dispose();
        return null;
    }

    internal static bool IsRunning(string role)
    {
        using var available = TryAcquire(role);
        return available is null;
    }

    internal static bool WaitForExit(string role, TimeSpan timeout)
    {
        using var mutex = new Mutex(false, Name(role));
        bool acquired;
        try { acquired = mutex.WaitOne(timeout); }
        catch (AbandonedMutexException) { acquired = true; }
        if (acquired) mutex.ReleaseMutex();
        return acquired;
    }

    public void Dispose()
    {
        if (_owned)
        {
            _mutex.ReleaseMutex();
            _owned = false;
        }
        _mutex.Dispose();
    }
}
