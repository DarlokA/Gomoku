using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GomokuGame.Visualizer.Core
{
    public class VisualizerSettings : INotifyPropertyChanged
    {
        private double _learningRate = 0.5;
        private int _maxIterations = 500;
        private double _tolerance = 0.001;
        private int _maxAttempts = 5;
        private int _aggregationN = 20;
        private bool _onlyOneStart = true;

        public double LearningRate
        {
            get => _learningRate;
            set { _learningRate = value; OnPropertyChanged(); }
        }

        public int MaxIterations
        {
            get => _maxIterations;
            set { _maxIterations = value; OnPropertyChanged(); }
        }

        public double Tolerance
        {
            get => _tolerance;
            set { _tolerance = value; OnPropertyChanged(); }
        }

        public int MaxAttempts
        {
            get => _maxAttempts;
            set { _maxAttempts = value; OnPropertyChanged(); }
        }

        public int AggregationN
        {
            get => _aggregationN;
            set { _aggregationN = value; OnPropertyChanged(); }
        }

        public bool OnlyOneStart
        {
            get => _onlyOneStart;
            set { _onlyOneStart = value; OnPropertyChanged(); }
        }

        private static string FolderPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", "Visualizer");

        private static string FilePath => Path.Combine(FolderPath, "settings.json");

        public static VisualizerSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new VisualizerSettings();

                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<VisualizerSettings>(json);
                return loaded ?? new VisualizerSettings();
            }
            catch
            {
                return new VisualizerSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Не роняем UI, если диск недоступен/занят файл — просто не сохранили на этот раз.
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
