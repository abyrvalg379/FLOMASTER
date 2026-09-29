using System.Windows;
using System.Windows.Input;
using FLOMASTER.Models;
using FLOMASTER.ViewModels;

namespace FLOMASTER
{
    /// <summary>
    /// Полноэкранный оверлей по Ctrl+Alt+F: плитки приложений, профилей и последних
    /// файлов. Клик = действие + закрытие. Esc или повторный хоткей — закрыть.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private readonly MainViewModel _vm;

        public OverlayWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;

            // пустые состояния секций
            ProfilesEmpty.Visibility = viewModel.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RecentEmpty.Visibility = viewModel.RecentFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PresetTile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Preset preset)
            {
                _vm.SelectedPreset = preset;
                _vm.LaunchCommand.Execute(null);
                Close();
            }
        }

        private void ProfileTile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Profile profile)
            {
                _vm.ApplyProfile(profile);
                _vm.LaunchCommand.Execute(null);
                Close();
            }
        }

        private void RecentTile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is string file)
            {
                _vm.OpenRecentFile(file);
                Close();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
