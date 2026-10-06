using GomokuGame.AI;
using GomokuGame.Models;
using GomokuGame.Services;
using GomokuGame.Utils;
using GomokuGame.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GomokuGame
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _vm;
        private Cursor? _cursorX;
        private Cursor? _cursorO;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = DataContext as MainViewModel;
            if (_vm == null) return;

            _vm.PropertyChanged += OnVmPropertyChanged;

            // Создаём курсоры один раз
            RebuildCursors();

            // Применяем под текущего игрока
            ApplyCursor();
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPlayer))
            {
                ApplyCursor();
            }
            else if (e.PropertyName == nameof(MainViewModel.XColor)
                  || e.PropertyName == nameof(MainViewModel.OColor))
            {
                // Цвета изменились — пересобираем курсоры
                RebuildCursors();
                ApplyCursor();
            }
        }

        private void RebuildCursors()
        {
            if (_vm == null) return;

            var xColor = (Color)ColorConverter.ConvertFromString(_vm.XColor);
            var oColor = (Color)ColorConverter.ConvertFromString(_vm.OColor);

            _cursorX?.Dispose();
            _cursorO?.Dispose();

            _cursorX = CursorFactory.CreatePlayerCursor('X', xColor);
            _cursorO = CursorFactory.CreatePlayerCursor('O', oColor);
        }

        private void ApplyCursor()
        {
            if (_vm == null) return;

            Cursor = _vm.CurrentPlayer == CellState.X ? _cursorX : _cursorO;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                _vm?.SaveCurrentLog();   // сохранить незавершённый лог
                GameSettings? settings = _vm?.Settings;
                if (settings != null)
                    SettingsService.Save(settings);
                AiPlayer? palyer = _vm?.Ai;
                if (palyer != null)
                    AiService.Save(palyer);
            }

            _cursorX?.Dispose();
            _cursorO?.Dispose();

            base.OnClosed(e);
        }
    }
}