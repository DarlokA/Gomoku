using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GomokuGame.Models
{
    public class GameSettings : INotifyPropertyChanged
    {
        private int _boardSize = 15;
        private int _winLength = 5;
        private string _xColor = "#1976D2";
        private string _oColor = "#D32F2F";
        
        private bool _enableGameLog = false;

        /// <summary>
        /// Вести лог партий в текстовый файл.
        /// </summary>
        public bool EnableGameLog
        {
            get => _enableGameLog;
            set
            {
                if (_enableGameLog == value) return;
                _enableGameLog = value;
                OnPropertyChanged();
            }
        }

        private string _trainerExePath = "";
        /// <summary>
        /// Путь к GomokuGame.Trainer.exe — запоминается между запусками игры.
        /// </summary>
        public string TrainerExePath
        {
            get => _trainerExePath;
            set
            {
                if (_trainerExePath == value) return;
                _trainerExePath = value;
                OnPropertyChanged();
            }
        }

        public int BoardSize
        {
            get => _boardSize;
            set
            {
                if (_boardSize == value) return;
                _boardSize = value;
                OnPropertyChanged();
            }
        }

        public int WinLength
        {
            get => _winLength;
            set
            {
                if (_winLength == value) return;
                _winLength = value;
                OnPropertyChanged();
            }
        }

        public string XColor
        {
            get => _xColor;
            set { if (_xColor == value) return; _xColor = value; OnPropertyChanged(); }
        }

        public string OColor
        {
            get => _oColor;
            set { if (_oColor == value) return; _oColor = value; OnPropertyChanged(); }
        }

        private bool _xIsComputer;
        private bool _oIsComputer;
        private string _selectedNetwork = "network_a_15_big";

        /// <summary>
        /// Крестики играет компьютер.
        /// </summary>
        public bool XIsComputer
        {
            get => _xIsComputer;
            set
            {
                if (_xIsComputer == value) return;
                _xIsComputer = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Нолики играет компьютер.
        /// </summary>
        public bool OIsComputer
        {
            get => _oIsComputer;
            set
            {
                if (_oIsComputer == value) return;
                _oIsComputer = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Имя выбранной сети (без .json). Файл: Documents\My Games\GomokuGame\{name}.json
        /// </summary>
        public string SelectedNetwork
        {
            get => _selectedNetwork;
            set
            {
                if (_selectedNetwork == value) return;
                _selectedNetwork = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}