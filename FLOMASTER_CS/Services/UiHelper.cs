using System;
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
                Height = owner.Height,
                Background = Brushes.Black,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Owner = owner
            };

            // Place beside the owner window: to the right if there is room, otherwise to the left
            var workArea = SystemParameters.WorkArea;
            const double gap = 8;
            double ownerRight = owner.Left + owner.Width;
            double spaceRight = workArea.Right - ownerRight;
            double spaceLeft = owner.Left - workArea.Left;
            double x;
            if (spaceRight >= logWindow.Width)
                x = ownerRight + gap;
            else if (spaceLeft >= logWindow.Width)
                x = owner.Left - gap - logWindow.Width;
            else if (spaceRight >= spaceLeft)
                x = Math.Min(ownerRight + gap, workArea.Right - logWindow.Width);
            else
                x = Math.Max(workArea.Left, owner.Left - gap - logWindow.Width);

            logWindow.Left = x;
            logWindow.Top = owner.Top;

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
