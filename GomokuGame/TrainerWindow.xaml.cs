using System.ComponentModel;
using System.Windows;
using GomokuGame.ViewModels;

namespace GomokuGame
{
    public partial class TrainerWindow : Window
    {
        private TrainerViewModel? _vm;

        public TrainerWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosingWindow;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = DataContext as TrainerViewModel;
            if (_vm != null)
                _vm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrainerViewModel.LogText))
            {
                LogScroll.ScrollToEnd();
            }
        }

        private void OnClosingWindow(object? sender, CancelEventArgs e)
        {
            _vm?.OnWindowClosing();
        }
    }
}