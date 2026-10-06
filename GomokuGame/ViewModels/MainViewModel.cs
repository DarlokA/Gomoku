using GomokuGame.AI;
using GomokuGame.Models;
using GomokuGame.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace GomokuGame.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private AiPlayer? _ai;

        public AiPlayer? Ai => _ai;

        private GameBoard _board;
        private readonly GameSettings _settings;

        public GameSettings Settings => _settings;

        private GameLogger? _logger;

        public ObservableCollection<CellViewModel> Cells { get; } = new();

        private CellState _currentPlayer = CellState.X;
        public CellState CurrentPlayer
        {
            get => _currentPlayer;
            set
            {
                if (SetProperty(ref _currentPlayer, value))
                {
                    OnPropertyChanged(nameof(CurrentPlayerDisplay));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string CurrentPlayerDisplay => CurrentPlayer == CellState.X ? "X" : "O";

        private string _statusText = "Ход игрока X";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isGameOver;
        public bool IsGameOver
        {
            get => _isGameOver;
            set => SetProperty(ref _isGameOver, value);
        }

        // Свойства для биндинга в XAML
        public int BoardSize => _settings.BoardSize;
        public string XColor => _settings.XColor;
        public string OColor => _settings.OColor;

        
        private bool IsCurrentPlayerComputer() =>
                        CurrentPlayer == CellState.X ? _settings.XIsComputer : _settings.OIsComputer;

        private bool _isComputerThinking;
        public bool IsComputerThinking
        {
            get => _isComputerThinking;
            set => SetProperty(ref _isComputerThinking, value);
        }


        public ICommand CellClickCommand { get; }
        public ICommand NewGameCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenTrainerCommand { get; }

        public MainViewModel()
        {
            _settings = SettingsService.Load();
            _settings.PropertyChanged += OnSettingsChanged;

            RebuildAi();   // ← вместо старого LoadOrCreate

            _board = new GameBoard(_settings.BoardSize, _settings.WinLength);

            CellClickCommand = new RelayCommand<CellViewModel>(
                OnCellClick, c => c != null && c.IsEmpty && !IsGameOver);
            NewGameCommand = new RelayCommand(NewGame);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenTrainerCommand = new RelayCommand(OpenTrainer);

            InitializeBoard();
        }

        private void RebuildAi()
        {
            _ai = null;

            if (_settings.XIsComputer || _settings.OIsComputer)
            {
                _ai = AiService.LoadOrCreateNamed(_settings.SelectedNetwork);
                _ai.Epsilon = 0.0;
                _ai.UseRotatingEvaluation = false;   // ← TTA при игре в UI
            }
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameSettings.BoardSize)
                || e.PropertyName == nameof(GameSettings.WinLength)
                || e.PropertyName == nameof(GameSettings.SelectedNetwork)
                || e.PropertyName == nameof(GameSettings.XIsComputer)
                || e.PropertyName == nameof(GameSettings.OIsComputer))
            {
                RebuildAi();
                NewGame();
            }
            else if (e.PropertyName == nameof(GameSettings.XColor)
                  || e.PropertyName == nameof(GameSettings.OColor))
            {
                OnPropertyChanged(nameof(XColor));
                OnPropertyChanged(nameof(OColor));
            }
        }

        private void InitializeBoard()
        {
            Cells.Clear();
            for (int r = 0; r < _settings.BoardSize; r++)
                for (int c = 0; c < _settings.BoardSize; c++)
                    Cells.Add(new CellViewModel(r, c));

            OnPropertyChanged(nameof(BoardSize));
        }

        private void OnCellClick(CellViewModel? cell)
        {
            if (cell == null || IsGameOver) return;
            if (IsComputerThinking) return;
            if (IsCurrentPlayerComputer()) return;   // ← если текущий — компьютер, игнор
            if (!cell.IsEmpty) return;

            MakeMove(cell);
        }

        public void SaveCurrentLog()
        {
            _logger?.Save();
        }

        private void MakeMove(CellViewModel cell)
        {
            cell.State = CurrentPlayer;
            _board[cell.Row, cell.Col] = CurrentPlayer;

            // Лог
            _logger?.LogMove(_board, CurrentPlayer, cell.Row, cell.Col);

            var winLine = _board.GetWinningLine(cell.Row, cell.Col, CurrentPlayer);
            if (winLine != null)
            {
                HighlightWinningLine(winLine);
                StatusText = StatusStrings.Win(CurrentPlayer);
                IsGameOver = true;

                _logger?.LogResult(CurrentPlayer, _board.GetMoveCount());   // ← нужен счётчик ходов
                _logger?.Save();

                FinishGame(CurrentPlayer);
                return;
            }

            if (_board.IsFull())
            {
                StatusText = StatusStrings.Draw;
                IsGameOver = true;

                _logger?.LogResult(CellState.Empty, _board.GetMoveCount());
                _logger?.Save();

                FinishGame(CellState.Empty);
                return;
            }

            CurrentPlayer = CurrentPlayer == CellState.X ? CellState.O : CellState.X;
            StatusText = StatusStrings.Turn(CurrentPlayer);
            ScheduleComputerMoveIfNeeded();
        }

        private void FinishGame(CellState winner)
        {
            if (_ai == null) return;
            if (_ai.HistoryCount == 0) return;

            _ai.LearnFromGame(winner, _settings.WinLength);
            _ai.Save();
        }

        private void ScheduleComputerMoveIfNeeded()
        {
            if (IsGameOver) return;
            if (!IsCurrentPlayerComputer()) return;   // ← если человек — выходим

            IsComputerThinking = true;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                IsComputerThinking = false;
                MakeComputerMove();
            };
            timer.Start();
        }

        private void MakeComputerMove()
        {
            if (IsGameOver) return;
            if (!IsCurrentPlayerComputer()) return;
            if (_ai == null) return;

            var move = _ai.ChooseMove(_board, CurrentPlayer);
            if (move == null) return;

            var (row, col) = move.Value;
            var player = CurrentPlayer;
            var opponent = player == CellState.X ? CellState.O : CellState.X;

            var bestCandidate = _ai.LastBestCandidate;
            var mostCritical = _ai.LastMostCritical;

            // bestCandidate == null означает ε-greedy случайный ход — как и в PlayOneGame,
            // такой ход не несёт полезного обучающего сигнала и не записывается.
            if (bestCandidate != null)
            {
                bool missedWin = MoveCriticality.MissedWin(_board, bestCandidate.Row, bestCandidate.Col, player, _settings.WinLength);
                bool threatWasVisible = mostCritical != null && mostCritical.Criticality >= 0.9;
                bool playedCorrectly = bestCandidate.Criticality >= 0.9;

                MoveCandidate? sampleToRecord;
                double weight;
                bool isForcedCorrection;

                if (missedWin)
                {
                    sampleToRecord = mostCritical;
                    weight = 5.0;
                    isForcedCorrection = true;
                }
                else if (threatWasVisible && !playedCorrectly)
                {
                    sampleToRecord = mostCritical;
                    isForcedCorrection = true;

                    // Гонка: вместо блока сеть разогнала свою линию до того же уровня
                    // критичности (need=1), хотя следующий ход достанется противнику —
                    // то есть выбрала ход, который выглядит как "я тоже скоро выиграю",
                    // но на деле уже проиграла. Строго худшая ошибка, чем обычный
                    // пропуск защиты без такой иллюзорной альтернативы.
                    _board[bestCandidate.Row, bestCandidate.Col] = player;
                    double ownPotentialAfterChosen =
                        _board.GetLinePotential(bestCandidate.Row, bestCandidate.Col, player, _settings.WinLength);
                    _board[bestCandidate.Row, bestCandidate.Col] = CellState.Empty;

                    bool isRaceMistake = ownPotentialAfterChosen >= 5.0;
                    weight = isRaceMistake ? 8.0 : 5.0;
                }
                else if (playedCorrectly)
                {
                    sampleToRecord = bestCandidate;
                    weight = 3.0;
                    isForcedCorrection = false;
                }
                else
                {
                    sampleToRecord = bestCandidate;
                    weight = 1.0;
                    isForcedCorrection = false;
                }

                if (sampleToRecord != null)
                {
                    int sr = sampleToRecord.Row;
                    int sc = sampleToRecord.Col;

                    _board[sr, sc] = player;
                    int sampleLineLen = _board.GetLineLengthAt(sr, sc, player);
                    double sampleMyPotential = _board.GetLinePotential(sr, sc, player, _settings.WinLength);
                    double sampleOppPotential = _board.GetMaxPotential(opponent, _settings.WinLength);
                    _board[sr, sc] = CellState.Empty;

                    _ai.RecordMoveWithWeight(
                        sampleToRecord.State,
                        sr, sc,
                        player,
                        sampleLineLen,
                        sampleMyPotential,
                        sampleOppPotential,
                        weight,
                        criticality: sampleToRecord.Criticality,
                        isForcedCorrection: isForcedCorrection);
                }
            }

            var cell = Cells.First(c => c.Row == row && c.Col == col);
            MakeMove(cell);
        }



        private void HighlightWinningLine(List<(int Row, int Col)> line)
        {
            foreach (var (r, c) in line)
            {
                var cellVm = Cells.FirstOrDefault(x => x.Row == r && x.Col == c);
                if (cellVm != null) cellVm.IsWinning = true;
            }
        }

        private void NewGame()
        {
            _ai?.ResetHistory();
            _board = new GameBoard(_settings.BoardSize, _settings.WinLength);
            InitializeBoard();

            IsGameOver = false;
            CurrentPlayer = CellState.X;
            StatusText = StatusStrings.Turn(CellState.X);

            // Логгер
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", "logs");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            _logger?.Save();   // сохранить предыдущий лог, если был
            _logger = null;

            if (_settings.EnableGameLog)
            {
                logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My Games", "GomokuGame", "logs");
                Directory.CreateDirectory(logDir);
                logPath = Path.Combine(logDir, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                _logger = new GameLogger(logPath);
                _logger.LogHeader(
                    _settings.BoardSize, _settings.WinLength,
                    _settings.XIsComputer ? _settings.SelectedNetwork : "Human",
                    _settings.OIsComputer ? _settings.SelectedNetwork : "Human");
            }

            ScheduleComputerMoveIfNeeded();
        }


        private void OpenSettings()
        {
            var vm = new SettingsViewModel(_settings);

            var window = new SettingsWindow
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current.MainWindow
            };

            vm.RequestClose += result =>
            {
                if (result == true)
                {
                    vm.ApplyTo(_settings);
                    SettingsService.Save(_settings);   // ← сохраняем на диск
                }
                window.Close();
            };

            window.ShowDialog();
        }

        private void OpenTrainer()
        {
            var vm = new TrainerViewModel(_settings);
            var window = new TrainerWindow
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current.MainWindow
            };
            // Немодальное окно: можно свернуть и продолжить играть, пока трейнер работает
            window.Show();
        }
    }
}