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

using ETS2Trainer.Core;

namespace ETS2Trainer;

internal sealed class TrainerLifetimeControl : IDisposable
{
    private readonly EventWaitHandle _quit = new(false, EventResetMode.AutoReset, TrainerInstance.Name("QuitDesktop"));
    private readonly RegisteredWaitHandle _wait;
    private bool _disposed;

    internal TrainerLifetimeControl(MainWindow window)
    {
        _wait = ThreadPool.RegisterWaitForSingleObject(_quit, (_, _) =>
        {
            window.Dispatcher.InvokeAsync(() =>
            {
                if (!_disposed) window.QuitTrainer();
            });
        }, null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wait.Unregister(null);
        _quit.Dispose();
    }
}
