using System.Windows;
using System.Windows.Controls;

namespace MusicSwitcher
{
    public partial class OpacitySliderWindow : Window
    {
        private readonly Action<double> _onApply;

        public OpacitySliderWindow(double currentOpacity, Action<double> onApply)
        {
            InitializeComponent();
            _onApply = onApply;
            OpacitySlider.Value = Math.Clamp(currentOpacity * 100, 30, 100);
            UpdateText();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateText();
            if (OpacitySlider.IsLoaded)
                _onApply(Math.Clamp(OpacitySlider.Value / 100.0, 0.3, 1.0));
        }

        private void UpdateText()
        {
            var pct = (int)OpacitySlider.Value;
            ValueText.Text = $"{pct}%";
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _onApply(OpacitySlider.Value / 100.0);
            Close();
        }
    }
}
