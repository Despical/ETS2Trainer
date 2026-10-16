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

using System.Windows.Threading;
using ETS2Trainer.Core;

namespace ETS2Trainer;

internal sealed class GameSessionController : IDisposable
{
    private readonly MainWindow _window;
    private readonly GameExitPolicy _policy = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool _reading;
    private bool _disposed;
    private int _preferenceGeneration;

    internal GameSessionController(MainWindow window)
    {
        _window = window;
        _window.AutoClosePreferenceChanged += OnPreferenceChanged;
        _timer.Tick += OnTick;
        _timer.Start();
        ObserveAsync();
    }

    private void OnTick(object? sender, EventArgs e) => ObserveAsync();

    private void OnPreferenceChanged(object? sender, EventArgs e)
    {
        _policy.Reset();
        _preferenceGeneration++;
        ObserveAsync();
    }

    private async void ObserveAsync()
    {
        if (_disposed || _reading) return;
        if (!_window.AutoCloseWithGame)
        {
            _policy.Reset();
            return;
        }
        _reading = true;
        var generation = _preferenceGeneration;
        try
        {
            var games = await Task.Run(GameAutoStartService.ReadGameProcesses);
            if (!_disposed && generation == _preferenceGeneration && _policy.Observe(_window.AutoCloseWithGame, games))
                _window.QuitTrainer();
        }
        finally { _reading = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _window.AutoClosePreferenceChanged -= OnPreferenceChanged;
    }
}
