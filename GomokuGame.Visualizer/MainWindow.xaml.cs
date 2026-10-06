using System.ComponentModel;
using System.Windows;
using GomokuGame.Visualizer.Rendering;

namespace GomokuGame.Visualizer
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Redraw();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.LastResult) ||
                e.PropertyName == nameof(MainViewModel.Target))
            {
                Redraw();
            }
        }

        private void Redraw()
        {
            var probs = _viewModel.LastResult?.Probs;
            double target = _viewModel.Target;

            BoardRenderer.Render(CanvasAll, probs, LayerMode.All, target);
            BoardRenderer.Render(CanvasOwn, probs, LayerMode.Own, target);
            BoardRenderer.Render(CanvasOpponent, probs, LayerMode.Opponent, target);
            BoardRenderer.Render(CanvasBorder, probs, LayerMode.Border, target);
        }
    }
}