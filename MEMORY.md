# MEMORY.md — Контекст проекта Gomoku

## Проект
**Гомоку** — игра "5 в ряд" с нейросетевым обучением.  
.NET 8.0 WPF, Windows x64.

### Структура
| Каталог | Назначение |
|---|---|
| `GomokuGame/` | Клиентское WPF-приложение (UI), AI-движок, сетевые сервисы |
| `GomokuGame.Trainer/` | Консольный тренер (`GomokuGame.Trainer.exe`) — обучение и матчи |
| `GomokuGame.Trainer.MCP/` | MCP-сервер для AI-интеграции |
| `GomokuGame.Visualizer/` | Визуализация |
| `GomokuGame.Tests/` | Юнит-тесты |
| `GomokuGame.CriticalSamplesBrowser/` | Утилита просмотра критических сэмплов |

### Ключевые файлы
- `GomokuGame/ViewModels/TrainerViewModel.cs` — UI-логика запуска тренера
- `GomokuGame/Services/TrainerRunner.cs` — запуск внешнего процесса `GomokuGame.Trainer.exe`
- `GomokuGame/Views/Trainer/TrainerWindow.xaml` — окно тренера
- `GomokuGame.Trainer/Program.cs` — точка входа тренера
- `GomokuGame.Trainer/ArgumentParser.cs` — парсер `ключ:значение`
- `GomokuGame.Trainer/TrainingArgumentsParser.cs` — парсер аргументов `train`
- `GomokuGame.Trainer/MatchArgumentsParser.cs` — парсер аргументов `match`
- `GomokuGame.Trainer/readme.txt` — документация по CLI (авторитетный источник)
- `GomokuGame/Models/GameSettings.cs` — настройки игры (доска, выигрышная линия)
- `GomokuGame/AI/AiPlayer.cs` — основной ИИ-игрок
- `GomokuGame/AI/Training/` — интерфейсы и реализация тренеров

---

## Командная строка тренера (CLI)

**Формат:** `GomokuGame.Trainer.exe <команда> ключ:значение ...`

Все аргументы — в формате `ключ:значение` (case-insensitive, разделитель `:`).

### Команды

| Команда | Описание |
|---|---|
| `train` | Обучение сети |
| `match` | Матч между двумя сетями |
| `matchlog` | Матч с логированием партий в файл |

### Общие аргументы
- `consolelog:<путь>` — зеркалирование вывода консоли в файл

### `train algorithm:standard` / `train algorithm:bot`
`board:N` `win:N` `games:N` `lr:double` `epsilon:double` `decay:double` `mode:A|B|C|D|E` `correction:Full|Once|Off|All` `chunk:N` `save:имя` `demo:1|0`

### `train algorithm:league`
+ `warmup:N` `snapshotEvery:N`

### `train algorithm:genetic`
`board:N` `win:N` `chunk:N` `mode:A|B|C|D|E` `population:N` `eliteCount:N` `mutationSigma:double` `gamesPerPair:N` `cycles:N` `save:имя`

### `match`
`board:N` `win:N` `games:N` `x:имя` `o:имя`

### `matchlog`
`board:N` `win:N` `x:имя` `o:имя` `epsilon:double` `log:<путь>`

### Режимы обучения (mode)
- **A** — LineLengthOnly (длина линии)
- **B** — DiscountedOutcome (дисконт от победы)
- **C** — DiscountedWithShaping (дисконт + shaping) — по умолчанию
- **D** — AttackDefenseShaping (атака/защита)
- **E** — LocalTactical (LTT, локальная тактика)

### Режимы коррекции (correction)
- **FULL** — полная коррекция
- **ONCE** — коррекция один раз
- **OFF** — отключить
- **ALL** — все веса сразу

---

## Архитектура запуска тренера из UI

```
TrainerWindow.xaml (View)
    ↓ DataBinding
TrainerViewModel.cs (ViewModel)
    ↓ BuildArgs() → List<string> ["match", "board:9", "win:5", ...]
TrainerRunner.cs (Service)
    ↓ ProcessStartInfo.ArgumentList.Add()
GomokuGame.Trainer.exe
```

### Важные детали
1. `TrainerViewModel.BuildArgs()` формирует аргументы в формате `ключ:значение`
2. `TrainerRunner` использует `ArgumentList` — экранирование не требуется
3. Дробные числа форматируются через `CultureInfo.InvariantCulture` (точка вместо запятой)
4. При перенаправлении вывода (из UI) тренер переключает `Console.OutputEncoding` на UTF-8, чтобы русский текст не ломался

### Кодировка консоли (критично!)
- Когда вывод перенаправлен (`Console.IsOutputRedirected == true`), .NET по умолчанию использует OEM-кодировку (CP866 для русской локали)
- UI читает stdout как UTF-8 → русские символы превращаются в "кракозябры"
- **Решение** в `Program.cs`: при `IsOutputRedirected` принудительно ставим `UTF8`
- Интерактивная консоль (без перенаправления) — не меняется, работает нативно

---

## Конвенции
- Комментарии в коде — на русском языке
- Имена файлов и классов — CamelCase / PascalCase
- Настройки сохраняются через `SettingsService.Save()`
- UI использует MVVM с WPF DataBinding
