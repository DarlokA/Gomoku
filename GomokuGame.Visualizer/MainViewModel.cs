using GomokuGame.AI;
using GomokuGame.Services;
using GomokuGame.Visualizer.Core;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
namespace GomokuGame.Visualizer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private NeuralNetwork? _network = null;
        private string _networkPath = "";
        private double _target = 0.5;
        private string _status = "Сеть не загружена";
        private ReconstructionResult? _lastResult;

        public VisualizerSettings Settings { get; }

        public double Target
        {
            get => _target;
            set { _target = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public ReconstructionResult? LastResult
        {
            get => _lastResult;
            private set { _lastResult = value; OnPropertyChanged(); }
        }

        public bool IsNetworkLoaded => _network != null;

        public RelayCommand LoadNetworkCommand { get; }
        public RelayCommand ReconstructCommand { get; }
        public RelayCommand RestartCommand { get; }
        public RelayCommand SaveSettingsCommand { get; }

        private int _restartSeed = 0;

        public MainViewModel()
        {
            Settings = VisualizerSettings.Load();
            Settings.PropertyChanged += (_, _) => Settings.Save();

            LoadNetworkCommand = new RelayCommand(_ => LoadNetwork());
            ReconstructCommand = new RelayCommand(_ => RunReconstruction(autoRetry: true), _ => IsNetworkLoaded);
            RestartCommand = new RelayCommand(_ => RunReconstruction(autoRetry: false, forceNewSeed: true), _ => IsNetworkLoaded);
            SaveSettingsCommand = new RelayCommand(_ => Settings.Save());
        }

        private void LoadNetwork()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Файлы сети (*.json)|*.json|Все файлы (*.*)|*.*",
                Title = "Загрузить нейросеть"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var aiPlayer = NetworkSerializer.Load(dialog.FileName);
                _network = aiPlayer?.Network;
                _networkPath = dialog.FileName;

                if (_network?.OutputSize != 1)
                {
                    _network = null;
                    Status = $"Сеть не подходит: {_network?.OutputSize ?? 0} выходов вместо 1";
                    OnPropertyChanged(nameof(IsNetworkLoaded));
                    return;
                }

                Status = $"Загружена: {System.IO.Path.GetFileName(dialog.FileName)}";
                OnPropertyChanged(nameof(IsNetworkLoaded));
            }
            catch (Exception ex)
            {
                _network = null;
                Status = $"Ошибка загрузки: {ex.Message}";
                OnPropertyChanged(nameof(IsNetworkLoaded));
            }
        }

        private void RunReconstruction(bool autoRetry, bool forceNewSeed = false)
        {
            if (_network == null) return;

            Status = "Подбираю вход...";

            ReconstructionResult? result;
            if (autoRetry)
            {
                result = InputReconstructor.ReconstructWithRetries(
                    _network, Target,
                    maxAttempts: Settings.MaxAttempts,
                    maxIterations: Settings.MaxIterations,
                    learningRate: Settings.LearningRate,
                    tolerance: Settings.Tolerance);
            }
            else
            {
                _restartSeed++;
                result = InputReconstructor.Reconstruct(
                    _network, Target,
                    maxIterations: Settings.MaxIterations,
                    learningRate: Settings.LearningRate,
                    tolerance: Settings.Tolerance,
                    seed: _restartSeed);
            }

            LastResult = result;

            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", "Visualizer", "reconstruction_log.csv");

            if (result != null)
            {
                ReconstructionLogger.Append(logPath, Path.GetFileNameWithoutExtension(_networkPath), Target,
                                                autoRetry ? -1 : _restartSeed, result);
                Status = result.Converged
                        ? $"Сошлось за {result.Iterations} итераций, выход = {result.FinalOutput:F4}"
                        : $"Не сошлось (лучший выход = {result.FinalOutput:F4}), попробуй другой старт";
            }
            else
                Status = $"result == null!!!";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}