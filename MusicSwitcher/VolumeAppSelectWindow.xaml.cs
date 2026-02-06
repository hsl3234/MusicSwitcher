using System.Windows;
using System.Windows.Controls;
using MusicSwitcher.Services;

namespace MusicSwitcher
{
    public partial class VolumeAppSelectWindow : Window
    {
        public string? SelectedProcessName { get; private set; }
        private readonly IVolumeService _volumeService;

        public VolumeAppSelectWindow(IVolumeService volumeService)
        {
            InitializeComponent();
            _volumeService = volumeService;
            RefreshList();
        }

        private void RefreshList()
        {
            var sessions = _volumeService.GetAudioSessions();
            SessionList.ItemsSource = sessions;
            if (sessions.Count > 0)
            {
                SessionList.SelectedIndex = 0;
                EmptyHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyHint.Visibility = Visibility.Visible;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is VolumeSessionInfo info)
            {
                SelectedProcessName = info.ProcessName;
                DialogResult = true;
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
