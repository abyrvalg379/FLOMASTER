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
        // «STL» бывает именем проекта, а не форматом — не повторить эту путаницу).
        public static readonly string[] ProjectFileExtensions =
            { ".blend", ".spp", ".ma", ".mb", ".hip", ".hipl", ".hipnc", ".nk" };

        /// <summary>
        /// Тематический диалог ввода (замена серого VB InputBox). Возвращает строку
        /// или null при отмене. Enter = OK, Esc = отмена.
        /// </summary>
        public static string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var dlg = new Window
            {
                Title = title,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
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

            ApplySatelliteChrome(dlg, title);
            dlg.ShowDialog();
            return confirmed ? input.Text : null;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pref, int size);
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwcpRound = 2;

        /// <summary>Скругление углов окна (Windows 11): на Win10 API молча игнорируется.</summary>
        public static void RoundCorners(Window w)
        {
            try
            {
                void Apply(object s, EventArgs e)
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                    if (handle == IntPtr.Zero) return;
                    int pref = DwmwcpRound;
                    DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref pref, sizeof(int));
                }
                var existing = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (existing != IntPtr.Zero) Apply(null, null);   // handle уже есть (SourceInitialized уже сработал)
                else w.SourceInitialized += Apply;
            }
            catch { /* Win10 и старше — просто острые углы */ }
        }

        /// <summary>
        /// Окна-спутники в теме главного окна: своя полоса заголовка (название + крестик),
        /// без системной рамки, с тонкой обводкой. Контент переносится под полосу.
        /// </summary>
        private static void ApplySatelliteChrome(Window w, string title)
        {
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;

            var caption = new Grid { Height = 32, Background = (Brush)Application.Current.Resources["PanelBrush"] };
            var label = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                FontSize = 10
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            caption.Children.Add(label);

            var close = new Button
            {
                Content = "\uE8BB",
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Width = 40,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            close.SetResourceReference(Button.BackgroundProperty, "PanelBrush");
            close.SetResourceReference(Button.ForegroundProperty, "TextBrush");
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close, true);
            close.Click += (_, _) => w.Close();
            caption.Children.Add(close);

            w.AllowsTransparency = true;
            RoundCorners(w);

            var chrome = new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 32,
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                ResizeBorderThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            };
            System.Windows.Shell.WindowChrome.SetWindowChrome(w, chrome);
            RoundCorners(w);

            var oldContent = w.Content;
            w.Content = null;
            var border = new System.Windows.Controls.Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
                CornerRadius = new CornerRadius(10),
                Child = oldContent as System.Windows.UIElement
            };
            w.SizeChanged += (_, e) =>
                border.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 10, 10);
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(caption, Dock.Top);
            dock.Children.Add(caption);
            if (border.Child != null) dock.Children.Add(border);
            w.Content = dock;
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
            ApplySatelliteChrome(logWindow, "FLOMASTER — Log");
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
            ApplySatelliteChrome(picker, "Colorspace — " + roleName);
            return picker;
        }
    }
}
