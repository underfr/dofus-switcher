using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DofusSwitcher.Services;

namespace DofusSwitcher.Converters;

/// <summary>Convertit un IconIndex (int) en ImageSource pour les icônes de presets.</summary>
[ValueConversion(typeof(int), typeof(ImageSource))]
public class PresetIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int idx && idx >= 0)
            return SpriteHelper.LoadPresetIcon(idx);
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
