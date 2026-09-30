using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FLOMASTER.Services
{
    /// <summary>
    /// Применение темы на уровень приложения (Application.Current.Resources).
    /// Живёт отдельно от MainWindow: при старте с дашборда главного окна может не быть.
    /// </summary>
    public static class ThemeApplier
    {
        public static void Apply(string themeName)
        {
            var t = ThemeManager.GetTheme(
                ThemeManager.ThemeOrder.FirstOrDefault(k => ThemeManager.Themes[k].Name == themeName) ?? "blender"
            );
            Application.Current.Resources["BgBrush"] = ThemeManager.Brush(t.Bg);
            Application.Current.Resources["PanelBrush"] = ThemeManager.Brush(t.Panel);
            Application.Current.Resources["AccentBrush"] = ThemeManager.Brush(t.Accent);
            Application.Current.Resources["AccentTextBrush"] = ThemeManager.Brush(string.IsNullOrEmpty(t.AccentText) ? "#FFFFFF" : t.AccentText);
            Application.Current.Resources["TextBrush"] = ThemeManager.Brush(t.Text);
            Application.Current.Resources["DimBrush"] = ThemeManager.Brush(t.Dim);
            Application.Current.Resources["BorderBrush"] = ThemeManager.Brush(t.Border);

            var accentColor = (Color)ColorConverter.ConvertFromString(t.Accent);
            Application.Current.Resources["AccentHoverBrush"] = new SolidColorBrush(
                Color.FromArgb(0x55, accentColor.R, accentColor.G, accentColor.B));
            Application.Current.Resources["AccentPressBrush"] = new SolidColorBrush(
                Color.FromArgb(0x77, accentColor.R, accentColor.G, accentColor.B));
            Application.Current.Resources["AccentLightBrush"] = new SolidColorBrush(ShiftColor(accentColor, 1.18));
            Application.Current.Resources["AccentDarkBrush"] = new SolidColorBrush(ShiftColor(accentColor, 0.82));

            // System color overrides for ComboBox dropdowns
            Application.Current.Resources[System.Windows.SystemColors.WindowBrushKey] = ThemeManager.Brush(t.Panel);
            Application.Current.Resources[System.Windows.SystemColors.WindowTextBrushKey] = ThemeManager.Brush(t.Text);
            Application.Current.Resources[System.Windows.SystemColors.ControlBrushKey] = ThemeManager.Brush(t.Panel);
            Application.Current.Resources[System.Windows.SystemColors.ControlTextBrushKey] = ThemeManager.Brush(t.Text);
            Application.Current.Resources[System.Windows.SystemColors.HighlightBrushKey] = ThemeManager.Brush(t.Accent);
            Application.Current.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = ThemeManager.Brush(string.IsNullOrEmpty(t.AccentText) ? "#FFFFFF" : t.AccentText);
        }

        private static Color ShiftColor(Color c, double k)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, c.R * k),
                (byte)Math.Min(255, c.G * k),
                (byte)Math.Min(255, c.B * k));
        }
    }
}
