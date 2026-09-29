using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class UiHelper
    {
        // Расширения файлов проектов: источник для drag&drop, Recent и панели Projects.
        // Только родные форматы DCC. .fbx/.obj/.stl — обмен/выпечка, НЕ проекты (решение юзера;
        // «STL» в пути E:\...\VVERH\STL — это имя проекта, а не формат — не повторить мою путаницу).
        public static readonly string[] ProjectFileExtensions =
            { ".blend", ".spp", ".ma", ".mb", ".hip", ".hipl", ".hipnc", ".nk" };

        /// <summary>
        /// Тематический диалог ввода (замена серого VB InputBox). Возвращает строку
        /// или null при отмене. Enter = OK, Esc = отмена.
        /// </summary>
        public static string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            string result = null;
            var dlg = new Window
            {
                Title = title,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.Transparent,
                ShowInTaskbar = false
            };
            dlg.SetResourceReference(Window.BackgroundProperty, "BgBrush");

            var input = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12
            };
            input.SetResourceReference(TextBox.ForegroundProperty, "TextBrush");
            input.SetResourceReference(TextBox.BackgroundProperty, "BgBrush");
            input.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
            input.SetResourceReference(TextBox.CaretBrushProperty, "TextBrush");

            TextBlock Label(string key)
            {
                var tb = new TextBlock { FontSize = 11 };
                tb.SetResourceReference(TextBlock.ForegroundProperty, key);
                return tb;
            }

            var promptLabel = Label("DimBrush");
            promptLabel.Text = prompt;

            // BtnStyle живёт на уровне приложения — диалогам он доступен так же, как MainWindow
            var btnStyle = (Style)Application.Current.Resources["BtnStyle"];
            var ok = new Button { Content = "OK", Width = 90, Style = btnStyle };
            var cancel = new Button { Content = "Cancel", Width = 90, Style = btnStyle };

            bool confirmed = false;
            void Confirm()
            {
                confirmed = true;
                dlg.Close();
            }

            ok.Click += (_, _) => Confirm();
            cancel.Click += (_, _) => dlg.Close();
            input.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { Confirm(); e.Handled = true; }
            };
            dlg.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) dlg.Close();
            };
            dlg.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            cancel.Margin = new Thickness(8, 0, 0, 0);

            var content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(promptLabel);
            content.Children.Add(input);
            content.Children.Add(buttons);
            dlg.Content = content;

            dlg.ShowDialog();
            return confirmed ? input.Text : null;
        }

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

            PlaceBeside(logWindow, owner);

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

        /// Размещение рядом с владельцем: справа если влезает, иначе слева (как окно логов).
        private static void PlaceBeside(Window window, Window owner)
        {
            var workArea = SystemParameters.WorkArea;
            const double gap = 8;
            double ownerRight = owner.Left + owner.Width;
            double spaceRight = workArea.Right - ownerRight;
            double spaceLeft = owner.Left - workArea.Left;
            double x;
            if (spaceRight >= window.Width)
                x = ownerRight + gap;
            else if (spaceLeft >= window.Width)
                x = owner.Left - gap - window.Width;
            else if (spaceRight >= spaceLeft)
                x = Math.Min(ownerRight + gap, workArea.Right - window.Width);
            else
                x = Math.Max(workArea.Left, owner.Left - gap - window.Width);

            window.Left = x;
            window.Top = owner.Top;
        }

        /// <summary>
        /// Пикер colorspaces для роли: окно рядом с лаунчером (высота и позиция следуют
        /// за окном лаунчера), поиск сверху, дерево групп по family (как Color Space menu
        /// в Blender), колесо мыши скроллит везде. Цвета — из ресурсов темы приложения,
        /// поэтому пикер меняется вместе со сменой темы. onPick(null) = вернуть из конфига.
        /// </summary>
        public static Window CreateColorspacePicker(Window owner, string roleName, string? currentName,
            Dictionary<string, string> colorspaces,
            Action<string?> onPick)
        {
            var picker = new Window
            {
                Title = $"Colorspace — {roleName}",
                Width = 340,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Owner = owner
            };
            picker.SetResourceReference(Window.BackgroundProperty, "BgBrush");

            // высота и позиция следуют за окном лаунчера
            void SyncWithOwner()
            {
                picker.Height = owner.Height;
                PlaceBeside(picker, owner);
            }
            SyncWithOwner();
            SizeChangedEventHandler onOwnerSize = (_, _) => SyncWithOwner();
            owner.SizeChanged += onOwnerSize;
            picker.Closed += (_, _) => owner.SizeChanged -= onOwnerSize;

            var search = new TextBox
            {
                Margin = new Thickness(8, 8, 8, 4),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12
            };
            search.SetResourceReference(TextBox.ForegroundProperty, "TextBrush");
            search.SetResourceReference(TextBox.BackgroundProperty, "BgBrush");
            search.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
            search.SetResourceReference(TextBox.CaretBrushProperty, "TextBrush");

            // без внешнего ScrollViewer: у TreeView свой скролл — колесо работает везде
            var tree = new TreeView
            {
                BorderThickness = new Thickness(0),
                Margin = new Thickness(8, 0, 8, 8)
            };
            tree.SetResourceReference(TreeView.BackgroundProperty, "BgBrush");

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(search, Dock.Top);
            dock.Children.Add(search);
            dock.Children.Add(tree);
            picker.Content = dock;

            TextBlock Label(string key, bool bold = false)
            {
                var tb = new TextBlock { FontSize = 11, FontWeight = bold ? FontWeights.Bold : FontWeights.Regular };
                tb.SetResourceReference(TextBlock.ForegroundProperty, key);
                return tb;
            }

            TreeViewItem LeafItem(string name)
            {
                var isCurrent = name == currentName;
                var item = new TreeViewItem { Tag = name };
                var label = Label("AccentBrush", isCurrent);
                label.Text = name;
                item.Header = label;
                item.Selected += (_, _) =>
                {
                    picker.Close();
                    onPick(name);
                };
                return item;
            }

            void Fill(string filter)
            {
                tree.Items.Clear();
                filter = filter.Trim();

                var inherit = new TreeViewItem();
                var inheritLabel = Label("DimBrush", currentName == null);
                inheritLabel.Text = "(config default)";
                inherit.Header = inheritLabel;
                inherit.Selected += (_, _) =>
                {
                    picker.Close();
                    onPick(null);
                };
                tree.Items.Add(inherit);

                if (filter.Length == 0)
                {
                    // дерево по family, все группы свернуты: Utility/Aliases -> Aliases ...
                    var familyItems = new Dictionary<string, TreeViewItem>(StringComparer.Ordinal);
                    foreach (var name in colorspaces.Keys)
                    {
                        var parts = (string.IsNullOrEmpty(colorspaces[name]) ? "Misc" : colorspaces[name]).Split('/');
                        TreeViewItem? parent = null;
                        var path = "";
                        foreach (var part in parts)
                        {
                            path = path == "" ? part : path + "/" + part;
                            if (!familyItems.TryGetValue(path, out var node))
                            {
                                var header = Label("DimBrush");
                                header.Text = part;
                                node = new TreeViewItem { Header = header, Focusable = false };
                                familyItems[path] = node;
                                (parent == null ? tree.Items : parent.Items).Add(node);
                            }
                            parent = node;
                        }
                        parent?.Items.Add(LeafItem(name));
                    }
                }
                else
                {
                    // поиск: плоский список совпадений по имени
                    foreach (var name in colorspaces.Keys
                                 .Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                        tree.Items.Add(LeafItem(name));
                }
            }

            search.TextChanged += (_, _) => Fill(search.Text);
            Fill("");
            return picker;
        }
    }
}
