using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProyExplorador.Helpers
{
    // ──────────────────────────────────────────────────────────────────────
    //  BoolToVisibilityConverter
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  InverseBoolConverter
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  NullOrEmptyToVisibilityConverter
    //  ConverterParameter="invert"  → muestra cuando NO está vacío
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEmpty  = string.IsNullOrEmpty(value as string);
            bool invert   = parameter?.ToString()?.ToLower() == "invert";
            bool visible  = invert ? !isEmpty : isEmpty;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  UsageToColorConverter  (barra de disco: verde/amarillo/rojo)
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(double), typeof(Brush))]
    public class UsageToColorConverter : IValueConverter
    {
        // Brushes frozen → sin allocations en cada conversión
        private static readonly SolidColorBrush GreenBrush  = MakeFrozen("#6BCB77");
        private static readonly SolidColorBrush YellowBrush = MakeFrozen("#FFE66D");
        private static readonly SolidColorBrush RedBrush    = MakeFrozen("#FF6B6B");

        private static SolidColorBrush MakeFrozen(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double pct) return new SolidColorBrush(Colors.Gray);
            return pct switch
            {
                < 60  => GreenBrush,
                < 80  => YellowBrush,
                _     => RedBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  ViewModeToVisibilityConverter  (Details | Grid)
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class ViewModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var mode  = value as string;
            var param = parameter as string;
            return mode == param ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  BoolToPlayPauseConverter
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(string))]
    public class BoolToPlayPauseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "⏸" : "▶";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  BoolToMuteIconConverter
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(string))]
    public class BoolToMuteIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? "🔇" : "🔊";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  StringToBrushConverter  ("#RRGGBB" → SolidColorBrush)
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(string), typeof(Brush))]
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value?.ToString() ?? "#666666");
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(Colors.Gray); }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  InitialConverter  (string → primera letra mayúscula para avatar)
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(string), typeof(string))]
    public class InitialConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string)
                ? "U"
                : value.ToString()![0].ToString().ToUpper();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  FileSizeConverter  (long bytes → "1.4 MB")
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(long), typeof(string))]
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not long bytes) return "—";
            if (bytes == 0) return "—";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  DateTimeAgoConverter  (DateTime → "Hace 3 días")
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(DateTime), typeof(string))]
    public class DateTimeAgoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dt) return string.Empty;
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1)  return "Ahora";
            if (diff.TotalHours   < 1)  return $"Hace {(int)diff.TotalMinutes} min";
            if (diff.TotalDays    < 1)  return $"Hace {(int)diff.TotalHours} h";
            if (diff.TotalDays    < 7)  return $"Hace {(int)diff.TotalDays} días";
            return dt.ToString("dd/MM/yyyy", culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  BoolToRowHeightConverter  (bool → GridLength)
    // ──────────────────────────────────────────────────────────────────────
    [ValueConversion(typeof(bool), typeof(GridLength))]
    public class BoolToRowHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
