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

using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ETS2Trainer.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding(nameof(UiLanguage.Language))
    {
        Source = UiLanguage.Current,
        Mode = BindingMode.OneWay,
        Converter = new TextConverter(text)
    }.ProvideValue(serviceProvider);

    private sealed class TextConverter(string key) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => UiLanguage.Current.Text(key);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
