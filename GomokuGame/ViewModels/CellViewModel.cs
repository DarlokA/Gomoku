using GomokuGame.Models;

namespace GomokuGame.ViewModels
{
    public class CellViewModel : ViewModelBase
    {
        private CellState _state;
        private bool _isWinning;

        public int Row { get; }
        public int Col { get; }

        public CellState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(Display));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public bool IsWinning
        {
            get => _isWinning;
            set => SetProperty(ref _isWinning, value);
        }

        public string Display => State switch
        {
            CellState.X => "X",
            CellState.O => "O",
            _ => string.Empty
        };

        public bool IsEmpty => State == CellState.Empty;

        public CellViewModel(int row, int col)
        {
            Row = row;
            Col = col;
            _state = CellState.Empty;
        }
    }
}