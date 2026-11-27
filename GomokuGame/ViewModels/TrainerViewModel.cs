using GomokuGame.Models;
using GomokuGame.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace GomokuGame.ViewModels
{
    public class TrainerViewModel : ViewModelBase
    {
        private readonly GameSettings _settings;
        private readonly TrainerRunner _runner = new();

        public List<string> Modes { get; } = new() { "A", "B", "C", "D", "E" };
        public List<string> Algorithms { get; } = new() { "standard", "bot", "league", "genetic" };
        public List<string> CorrectionModes { get; } = new() { "FULL", "ONCE", "OFF", "ALL" };
        public List<string> AvailableNetworks { get; }

        // --- Train / Match ---
        private bool _isTrainMode = true;
        public bool IsTrainMode
        {
            get => _isTrainMode;
            set
            {
                if (SetProperty(ref _isTrainMode, value))
                {
                    OnPropertyChanged(nameof(IsMatchMode));
                    UpdatePreview();
                }
            }
        }
        public bool IsMatchMode
        {
            get => !_isTrainMode;
            set => IsTrainMode = !value;
        }

        // --- Путь к exe ---
        public string TrainerExePath
        {
            get => _settings.TrainerExePath;
            set
            {
                if (_settings.TrainerExePath == value) return;
                _settings.TrainerExePath = value;
                OnPropertyChanged();
                UpdatePreview();
                SettingsService.Save(_settings);
            }
        }

        // --- Общие параметры поля ---
        private int _boardSize = 9;
        public int BoardSize { get => _boardSize; set { if (SetProperty(ref _boardSize, value)) UpdatePreview(); } }

        private int _winLength = 5;
        public int WinLength { get => _winLength; set { if (SetProperty(ref _winLength, value)) UpdatePreview(); } }

        private int _games = 10000;
        public int Games { get => _games; set { if (SetProperty(ref _games, value)) UpdatePreview(); } }

        private bool _isInfinite;
        public bool IsInfinite
        {
            get => _isInfinite;
            set
            {
                if (SetProperty(ref _isInfinite, value))
                {
                    OnPropertyChanged(nameof(IsGamesEditable));
                    UpdatePreview();
                }
            }
        }

        public bool IsGamesEditable => !IsInfinite;

        // --- Train-параметры ---
        private double _learningRate = 0.001;
        public double LearningRate { get => _learningRate; set { if (SetProperty(ref _learningRate, value)) UpdatePreview(); } }

        private double _epsilon = 0.3;
        public double Epsilon { get => _epsilon; set { if (SetProperty(ref _epsilon, value)) UpdatePreview(); } }

        private double _decay = 0.9995;
        public double Decay { get => _decay; set { if (SetProperty(ref _decay, value)) UpdatePreview(); } }

        private int _seed = 42;
        public int Seed { get => _seed; set { if (SetProperty(ref _seed, value)) UpdatePreview(); } }

        private string _mode = "D";
        public string Mode { get => _mode; set { if (SetProperty(ref _mode, value)) UpdatePreview(); } }

        private string _saveName = "network";
        public string SaveName { get => _saveName; set { if (SetProperty(ref _saveName, value)) UpdatePreview(); } }

        private bool _showDemo = true;
        public bool ShowDemo { get => _showDemo; set { if (SetProperty(ref _showDemo, value)) UpdatePreview(); } }

        // --- Genetic-параметры ---
        private int _population = 8;
        public int Population { get => _population; set { if (SetProperty(ref _population, value)) UpdatePreview(); } }

        private int _eliteCount = 4;
        public int EliteCount { get => _eliteCount; set { if (SetProperty(ref _eliteCount, value)) UpdatePreview(); } }

        private double _mutationSigma = 0.05;
        public double MutationSigma { get => _mutationSigma; set { if (SetProperty(ref _mutationSigma, value)) UpdatePreview(); } }

        private int _gamesPerPair = 100;
        public int GamesPerPair { get => _gamesPerPair; set { if (SetProperty(ref _gamesPerPair, value)) UpdatePreview(); } }

        private int _cycles = 3;
        public int Cycles { get => _cycles; set { if (SetProperty(ref _cycles, value)) UpdatePreview(); } }


        private int _chunk = 500;
        public int Chunk { get => _chunk; set { if (SetProperty(ref _chunk, value)) UpdatePreview(); } }

        private int _warmupGames = 500;
        public int WarmupGames { get => _warmupGames; set { if (SetProperty(ref _warmupGames, value)) UpdatePreview(); } }

        private int _snapshotEvery = 100;
        public int SnapshotEvery { get => _snapshotEvery; set { if (SetProperty(ref _snapshotEvery, value)) UpdatePreview(); } }

        private string _algorithm = "standard";
        public string Algorithm
        {
            get => _algorithm;
            set
            {
                if (SetProperty(ref _algorithm, value))
                {
                    OnPropertyChanged(nameof(IsLeagueMode));
                    OnPropertyChanged(nameof(IsGeneticMode));
                    UpdatePreview();
                }
            }
        }

        public bool IsGeneticMode => Algorithm == "genetic";
        public bool IsLeagueMode => Algorithm == "league";

        private string _correctionMode = "FULL";
        public string CorrectionMode
        {
            get => _correctionMode;
            set
            {
                if (SetProperty(ref _correctionMode, value))
                    UpdatePreview();
            }
        }

        // --- Match-параметры ---
        private string _fileA = "network";
        public string FileA { get => _fileA; set { if (SetProperty(ref _fileA, value)) UpdatePreview(); } }

        private string _fileB = "network";
        public string FileB { get => _fileB; set { if (SetProperty(ref _fileB, value)) UpdatePreview(); } }

        // --- Превью команды и лог ---
        private string _commandPreview = "";
        public string CommandPreview { get => _commandPreview; private set => SetProperty(ref _commandPreview, value); }

        private readonly StringBuilder _logBuilder = new();
        private string _logText = "";
        public string LogText { get => _logText; private set => SetProperty(ref _logText, value); }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanStop));
                }
            }
        }
        public bool CanStart => !IsRunning;
        public bool CanStop => IsRunning;

        public ICommand BrowseExeCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ClearLogCommand { get; }

        public TrainerViewModel(GameSettings settings)
        {
            _settings = settings;

            AvailableNetworks = AiService.GetAvailableNetworks();
            if (!AvailableNetworks.Contains("random"))
                AvailableNetworks.Insert(0, "random");

            if (string.IsNullOrWhiteSpace(_settings.TrainerExePath))
                _settings.TrainerExePath = TryGuessTrainerPath();

            BrowseExeCommand = new RelayCommand(BrowseExe);
            StartCommand = new RelayCommand(Start, () => CanStart);
            StopCommand = new RelayCommand(Stop, () => CanStop);
            ClearLogCommand = new RelayCommand(() => { _logBuilder.Clear(); LogText = ""; });

            _runner.OutputReceived += OnRunnerOutput;
            _runner.Exited += OnRunnerExited;
            _runner.ErrorOccurred += OnRunnerError;

            UpdatePreview();
        }

        private static string TryGuessTrainerPath()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate = Path.Combine(baseDir, "GomokuGame.Trainer.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
            return "";
        }

        private void BrowseExe()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "GomokuGame.Trainer.exe|GomokuGame.Trainer.exe|Исполняемые файлы (*.exe)|*.exe|Все файлы|*.*",
                Title = "Выберите GomokuGame.Trainer.exe"
            };
            if (dlg.ShowDialog() == true)
            {
                TrainerExePath = dlg.FileName;
            }
        }

        private List<string> BuildArgs()
        {
            var ci = CultureInfo.InvariantCulture;
            var args = new List<string>();

            if (IsMatchMode)
            {
                // match board:9 win:5 games:1000 x:network o:network
                args.Add("match");
                args.Add($"board:{BoardSize}");
                args.Add($"win:{WinLength}");
                args.Add($"games:{(IsInfinite ? 0 : Games)}");
                args.Add($"x:{FileA}");
                args.Add($"o:{FileB}");
            }
            else
            {
                // train algorithm:standard board:9 win:5 games:10000 lr:0.001 ...
                args.Add("train");
                args.Add($"algorithm:{Algorithm}");
                args.Add($"board:{BoardSize}");
                args.Add($"win:{WinLength}");
                args.Add($"games:{(IsInfinite ? 0 : Games)}");
                args.Add($"lr:{LearningRate.ToString(ci)}");
                args.Add($"epsilon:{Epsilon.ToString(ci)}");
                args.Add($"decay:{Decay.ToString(ci)}");
                args.Add($"seed:{Seed}");
                args.Add($"mode:{Mode}");
                args.Add($"save:{(string.IsNullOrWhiteSpace(SaveName) ? "network" : SaveName)}");
                args.Add($"demo:{(ShowDemo ? 1 : 0)}");
                args.Add($"chunk:{Chunk}");
                args.Add($"correction:{CorrectionMode}");

                // league-специфичные
                if (Algorithm == "league")
                {
                    args.Add($"warmup:{WarmupGames}");
                    args.Add($"snapshotEvery:{SnapshotEvery}");
                }

                // genetic-специфичные
                if (Algorithm == "genetic")
                {
                    args.Add($"population:{Population}");
                    args.Add($"eliteCount:{EliteCount}");
                    args.Add($"mutationSigma:{MutationSigma.ToString(ci)}");
                    args.Add($"gamesPerPair:{GamesPerPair}");
                    args.Add($"cycles:{Cycles}");
                }
            }
            return args;
        }

        private void UpdatePreview()
        {
            var args = BuildArgs();
            string exeName = string.IsNullOrWhiteSpace(TrainerExePath)
                ? "GomokuGame.Trainer.exe"
                : Path.GetFileName(TrainerExePath);
            var quoted = args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
            CommandPreview = exeName + " " + string.Join(" ", quoted);
        }

        private void Start()
        {
            UpdatePreview();
            if (string.IsNullOrWhiteSpace(TrainerExePath) || !File.Exists(TrainerExePath))
            {
                AppendLog("Не найден файл GomokuGame.Trainer.exe — укажи путь к нему через «Обзор».");
                return;
            }

            AppendLog("=== Запуск: " + CommandPreview + " ===");
            IsRunning = true;
            _runner.Start(TrainerExePath, BuildArgs().ToArray());
        }

        private void Stop()
        {
            _runner.Stop();
            AppendLog("=== Остановлено пользователем ===");
            IsRunning = false;
        }

        private void OnRunnerOutput(string line)
        {
            Application.Current.Dispatcher.Invoke(() => AppendLog(line));
        }

        private void OnRunnerExited(int code)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"=== Процесс завершён (код {code}) ===");
                IsRunning = false;
            });
        }

        private void OnRunnerError(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog("Ошибка запуска: " + message);
                IsRunning = false;
            });
        }

        private void AppendLog(string line)
        {
            _logBuilder.AppendLine(line);
            LogText = _logBuilder.ToString();
        }

        /// <summary>Вызывается из code-behind при закрытии окна.</summary>
        public void OnWindowClosing()
        {
            if (IsRunning)
                _runner.Stop();
        }
    }
}