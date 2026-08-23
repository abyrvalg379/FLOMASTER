using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FLOMASTER.Services
{
    public static class UiHelper
    {
        public static Window CreateLogWindow(string logContent, Window owner)
        {
            var logWindow = new Window
            {
                Title = "FLOMASTER — Log",
                Width = 700,
                Height = 400,
                Background = Brushes.Black,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner
            };

            var textBox = new TextBox
            {
                Text = logContent,
                IsReadOnly = true,
                Background = Brushes.Black,
                Foreground = Brushes.LimeGreen,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8)
            };

            logWindow.Content = textBox;
            return logWindow;
        }
    }
}
