using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MusicSwitcher.Model;
using MusicSwitcher.ViewModel;
using System.Windows.Forms;
using System.Windows.Interop;

namespace MusicSwitcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Animation Animation { get; set; }
        private WidgetSettings _settings;
        private System.Windows.Threading.DispatcherTimer _savePositionTimer;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _settings = WidgetSettings.Load();

            Opacity = Math.Clamp(_settings.Opacity, 0.3, 1.0);
            _settings.Opacity = Opacity;

            if (IsValidSavedPosition(_settings.WindowLeft, _settings.WindowTop))
            {
                Left = _settings.WindowLeft;
                Top = _settings.WindowTop;
            }
            else
            {
                PlaceOnMonitor(_settings.MonitorIndex);
            }

            _savePositionTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _savePositionTimer.Tick += (_, _) =>
            {
                _savePositionTimer.Stop();
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
                _settings.MonitorIndex = GetMonitorIndexAt(Left + Width / 2, Top + Height / 2);
                _settings.Save();
            };

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            LocationChanged += MainWindow_LocationChanged;
            DataContext = viewModel;
        }

        private const int ResizeMargin = 8;

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            WinApiLib.EnableResize(hwnd);
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WinApiLib.WM_NCHITTEST)
                return IntPtr.Zero;

            long lp = lParam.ToInt64();
            int screenX = (short)(lp & 0xFFFF);
            int screenY = (short)((lp >> 16) & 0xFFFF);
            var pt = new WinApiLib.POINT { X = screenX, Y = screenY };
            if (!WinApiLib.ScreenToClient(hwnd, ref pt))
                return IntPtr.Zero;

            if (!WinApiLib.GetClientRect(hwnd, out var rect))
                return IntPtr.Zero;

            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            int m = ResizeMargin;

            bool left = pt.X <= m;
            bool right = pt.X >= w - m;
            bool top = pt.Y <= m;
            bool bottom = pt.Y >= h - m;

            if (top && left) { handled = true; return new IntPtr(WinApiLib.HTTOPLEFT); }
            if (top && right) { handled = true; return new IntPtr(WinApiLib.HTTOPRIGHT); }
            if (bottom && left) { handled = true; return new IntPtr(WinApiLib.HTBOTTOMLEFT); }
            if (bottom && right) { handled = true; return new IntPtr(WinApiLib.HTBOTTOMRIGHT); }
            if (left) { handled = true; return new IntPtr(WinApiLib.HTLEFT); }
            if (right) { handled = true; return new IntPtr(WinApiLib.HTRIGHT); }
            if (top) { handled = true; return new IntPtr(WinApiLib.HTTOP); }
            if (bottom) { handled = true; return new IntPtr(WinApiLib.HTBOTTOM); }

            return IntPtr.Zero;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.Opacity = Opacity;
            _settings.MonitorIndex = GetMonitorIndexAt(Left + Width / 2, Top + Height / 2);
            _settings.Save();
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            _savePositionTimer.Stop();
            _savePositionTimer.Start();
        }

        private bool IsValidSavedPosition(double left, double top)
        {
            if (Screen.AllScreens.Length == 0) return false;
            var bounds = new System.Drawing.Rectangle((int)left, (int)top, (int)Width, (int)Height);
            foreach (var screen in Screen.AllScreens)
            {
                var work = screen.WorkingArea;
                if (work.IntersectsWith(bounds))
                    return true;
            }
            return false;
        }

        private int GetMonitorIndexAt(double x, double y)
        {
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].WorkingArea.Contains((int)x, (int)y))
                    return i;
            }
            return 0;
        }

        public void PlaceOnMonitor(int monitorIndex)
        {
            var screens = Screen.AllScreens;
            if (monitorIndex < 0 || monitorIndex >= screens.Length)
                monitorIndex = 0;
            var work = screens[monitorIndex].WorkingArea;
            Left = work.Right - Width - 5;
            Top = work.Bottom - Height - 5;
            _settings.MonitorIndex = monitorIndex;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.Save();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Animation = new Animation(this);
            BuildContextMenu();
            Hide();
        }

        private void BuildContextMenu()
        {
            var menu = (ContextMenu)Resources["Menu"];
            if (menu == null) return;

            var monitorItem = new MenuItem { Header = "Монитор" };
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var index = i;
                var label = screens.Length == 1 ? "Основной" : (i == 0 ? "Основной" : $"Монитор {i + 1}");
                var item = new MenuItem { Header = label, IsCheckable = true };
                if (index == _settings.MonitorIndex)
                    item.IsChecked = true;
                item.Click += (_, _) =>
                {
                    _settings.MonitorIndex = index;
                    PlaceOnMonitor(index);
                    _settings.Save();
                    foreach (var child in monitorItem.Items.OfType<MenuItem>())
                        child.IsChecked = child == item;
                };
                monitorItem.Items.Add(item);
            }
            menu.Items.Insert(0, monitorItem);

            var opacityItem = new MenuItem { Header = "Прозрачность" };
            foreach (var pct in new[] { 100, 90, 80, 70, 60, 50 })
            {
                var value = pct / 100.0;
                var item = new MenuItem { Header = $"{pct}%", IsCheckable = true };
                if (Math.Abs(Opacity - value) < 0.01)
                    item.IsChecked = true;
                item.Click += (_, _) =>
                {
                    Opacity = value;
                    _settings.Opacity = value;
                    _settings.Save();
                    UpdateOpacityMenuChecks(opacityItem);
                };
                opacityItem.Items.Add(item);
            }
            var customItem = new MenuItem { Header = "Настройка…" };
            customItem.Click += (_, _) => OpenOpacitySliderWindow();
            opacityItem.Items.Add(customItem);
            menu.Items.Insert(1, opacityItem);
        }

        private void UpdateOpacityMenuChecks(MenuItem opacityItem)
        {
            foreach (var child in opacityItem.Items.OfType<MenuItem>().Where(m => m.Header is string s && s.EndsWith("%")))
            {
                if (child.Header is string header && header.Length >= 2 && int.TryParse(header.AsSpan(0, header.Length - 1), out int pct))
                    child.IsChecked = Math.Abs(Opacity - pct / 100.0) < 0.01;
            }
        }

        private void OpenOpacitySliderWindow()
        {
            var sliderWindow = new OpacitySliderWindow(Opacity, value =>
            {
                Opacity = Math.Clamp(value, 0.3, 1.0);
                _settings.Opacity = Opacity;
                _settings.Save();
            });
            sliderWindow.Owner = this;
            sliderWindow.ShowDialog();
            UpdateOpacityMenuChecks((MenuItem)((ContextMenu)Resources["Menu"]).Items[1]);
        }

        private void Border_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.Source is FrameworkElement fe && fe.ContextMenu != null)
            {
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            }
        }

        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (IsSourceButton(e.OriginalSource as DependencyObject))
                return;
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw in some edge cases
            }
        }

        private static bool IsSourceButton(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is System.Windows.Controls.Button)
                    return true;
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }
            return false;
        }
        
        /// <summary> Установка элементов с треком на начальные позиции после обновления размера </summary>

        private void Track_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Canvas.SetLeft(Track, 0);
            Canvas.SetLeft(Track1, Track1.ActualWidth + 360);
            StartTrackAnimation();
        }

        /// <summary>
        /// Запуск или остановка анимации бегущей строки песни если она размер трека слишком большой
        /// </summary>
        private void StartTrackAnimation()
        {
            Track.BeginAnimation(Canvas.LeftProperty, null);
            Track1.BeginAnimation(Canvas.LeftProperty, null);
            if (Track.ActualWidth > 230)
            {
                Animation.UpdateTrackAnimation();
                Track.BeginAnimation(Canvas.LeftProperty, Animation.StartFirstTrack);
                Track1.BeginAnimation(Canvas.LeftProperty, Animation.StartSecondTrack);

            }
        }
        /// <summary> Открытие или скрыте формы через кнопку на панели </summary>
        private void TaskbarIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            Activate();
            if (this.IsVisible)
            {
                Topmost = false;
                BeginAnimation(TopProperty, Animation.OnHide);
            }
            else
            {
                Topmost = false;
                this.Show();
                BeginAnimation(TopProperty, Animation.OnShow);
                WinApiLib.HideFromAltTab(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }

        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("MusicSwitcher.exe");
            Process.GetCurrentProcess().Kill();
        }
    }
}