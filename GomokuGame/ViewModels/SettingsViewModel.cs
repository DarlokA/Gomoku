using GomokuGame.Models;
using GomokuGame.Services;
using System.Collections.Generic;
using System.Windows.Input;

namespace GomokuGame.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private static int maxWinLength(int boardSize) => System.Math.Min(7, boardSize);

        public bool EnableGameLog
        {
            get => _enableGameLog;
            set => SetProperty(ref _enableGameLog, value);
        }
        private bool _enableGameLog;

        public int BoardSize
        {
            get => _boardSize;
            set
            {
                if (SetProperty(ref _boardSize, value))
                {
                    if (WinLength > maxWinLength(value))
                        WinLength = maxWinLength(value);
                    OnPropertyChanged(nameof(MaxWinLength));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }
        private int _boardSize;

        public int WinLength
        {
            get => _winLength;
            set
            {
                int clamped = System.Math.Clamp(value, 3, maxWinLength(BoardSize));
                if (SetProperty(ref _winLength, clamped))
                    OnPropertyChanged(nameof(CanSave));
            }
        }
        private int _winLength;

        public IReadOnlyList<ColorItem> Palette => ColorPalette.Standard;

        public ColorItem XColor
        {
            get => _xColor;
            set => SetProperty(ref _xColor, value);
        }
        private ColorItem _xColor;

        public ColorItem OColor
        {
            get => _oColor;
            set => SetProperty(ref _oColor, value);
        }
        private ColorItem _oColor;

        public int MaxWinLength => maxWinLength(BoardSize);

        public bool CanSave => BoardSize >= 5 && BoardSize <= 30
                    && WinLength >= 3 && WinLength <= MaxWinLength;

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event System.Action<bool?>? RequestClose;


        public bool IsXHuman
        {
            get => !XIsComputer;
            set { if (value) XIsComputer = false; }
        }

        public bool IsXComputer
        {
            get => XIsComputer;
            set { if (value) XIsComputer = true; }
        }

        private bool XIsComputer
        {
            get => _xIsComputer;
            set
            {
                if (SetProperty(ref _xIsComputer, value))
                {
                    OnPropertyChanged(nameof(IsXHuman));
                    OnPropertyChanged(nameof(IsXComputer));
                    OnPropertyChanged(nameof(AnyComputer));
                }
            }
        }
        private bool _xIsComputer;

        public bool IsOHuman
        {
            get => !OIsComputer;
            set { if (value) OIsComputer = false; }
        }

        public bool IsOComputer
        {
            get => OIsComputer;
            set { if (value) OIsComputer = true; }
        }

        private bool OIsComputer
        {
            get => _oIsComputer;
            set
            {
                if (SetProperty(ref _oIsComputer, value))
                {
                    OnPropertyChanged(nameof(IsOHuman));
                    OnPropertyChanged(nameof(IsOComputer));
                    OnPropertyChanged(nameof(AnyComputer));
                }
            }
        }
        private bool _oIsComputer;

        public bool AnyComputer => XIsComputer || OIsComputer;

        public List<string> AvailableNetworks { get; }

        public string SelectedNetwork
        {
            get => _selectedNetwork;
            set => SetProperty(ref _selectedNetwork, value);
        }
        private string _selectedNetwork;

        // --- Конструктор ---

        public SettingsViewModel(GameSettings source)
        {
            _enableGameLog = source.EnableGameLog;
            _boardSize = source.BoardSize;
            _winLength = System.Math.Clamp(source.WinLength, 3, 9);
            _xColor = ColorPalette.FindByHex(source.XColor);
            _oColor = ColorPalette.FindByHex(source.OColor);

            _xIsComputer = source.XIsComputer;
            _oIsComputer = source.OIsComputer;
            _selectedNetwork = string.IsNullOrEmpty(source.SelectedNetwork)
                ? AiService.DefaultNetwork
                : source.SelectedNetwork;

            AvailableNetworks = AiService.GetAvailableNetworks();

            // Если выбранной сети нет в списке — добавим
            if (!((List<string>)AvailableNetworks).Contains(_selectedNetwork))
            {
                ((List<string>)AvailableNetworks).Add(_selectedNetwork);
            }

            SaveCommand = new RelayCommand(() => RequestClose?.Invoke(true), () => CanSave);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        // --- ApplyTo ---

        public void ApplyTo(GameSettings target)
        {
            target.EnableGameLog = EnableGameLog;
            target.BoardSize = BoardSize;
            target.WinLength = WinLength;
            target.XColor = XColor.Hex;
            target.OColor = OColor.Hex;
            target.XIsComputer = XIsComputer;
            target.OIsComputer = OIsComputer;
            target.SelectedNetwork = SelectedNetwork;
        }
    }
}