# GomokuGame.Trainer.MCP

**MCP-сервер для запуска обучения и анализа логов Gomoku Game Trainer**

---

## 🚀 Быстрый старт

```bash
# Сборка проекта
dotnet build --configuration Release

# Перенос config.json в папку с exe (билд-степ)
copy config.json "$(TargetDir)" >nul 2>&1
```

---

## 📋 Команды CLI

### **Команда `train` — ЗАПУСК ОБУЧЕНИЯ** (поддерживает все параметры тренера!)

```bash
# Обучить сеть D на 20000 партий
dotnet run train D 20000

# Обучить с параметрами через опции --key:value
dotnet run train D --games:50000 --lr:0.01 --epsilon:0.2 --seed:123

# С именем сохранения
dotnet run train D 20000 --save:network_A

# Все параметры тренера в формате key:value
dotnet run train D 20000 \
  --games:50000 \
  --lr:0.01 \
  --epsilon:0.3 \
  --decay:0.995 \
  --seed:42 \
  --save:network_A \
  --algorithm:standard \
  --mode:C \
  --correction:FULL \
  --chunk:500 \
  --warmup:500 \
  --snapshotEvery:100 \
  --demo:1 \
  --board:9 \
  --win:5
```

**Параметры команды `train`:**
- `<сеть>` — A/B/C/D/E (обязательно)
- `<партии>` — количество партий для обучения (по умолчанию 20,000)
- `[saveName]` — имя сохранения обученной сети (опционально)
- `[seed]` — seed случайных чисел (по умолчанию 42)
- `[learningRate]` — learning rate (по умолчанию 0.01)

**Дополнительные параметры через `--key:value`:**
- `games:N` — количество партий (по умолчанию 20,000)
- `lr:N` — learning rate (по умолчанию 0.01)
- `epsilon:N` — epsilon для exploration (по умолчанию 0.3)
- `decay:N` — decay coefficient (по умолчанию 0.995)
- `seed:N` — seed случайных чисел (по умолчанию 42)
- `save:имя` — имя сохранения сети
- `algorithm:` — алгоритм обучения (standard/league/genetic/bot)
- `mode:A|B|C|D|E` — режим обучения (LearningMode)
- `correction:FULL|ONCE|OFF|ALL` — коррекция ошибок
- `chunk:N` — размер чанка для обучения (по умолчанию 500)
- `warmup:N` — warmup партий (по умолчанию 500)
- `snapshotEvery:N` — снимки каждые N партий (по умолчанию 100)
- `demo:0|1` — показывать демо игры (по умолчанию 1)
- `board:N` — размер доски (по умолчанию 9x9)
- `win:N` — длина победы (по умолчанию 5)

### Анализ CSV-логов

```bash
# Автоматический анализ лога
dotnet run analyze <путь_к_логу>

# Или просто путь к лог (завершится после анализа)
dotnet run <путь_к_логу>
```

**Примеры:**
```bash
dotnet run analyze logs\pattern_log_network_a_2_40000.csv
dotnet run tests\baseline_d_20000_permuted.txt
```

### Управление обучением

```bash
# Остановить обучение
dotnet run stop

# Проверить статус (запущен/остановлен)
dotnet run status

# Показать информацию об окружении
dotnet run help
```

### Тестирование

```bash
# Запустить все тесты CsvLogParser
dotnet run --test
```

---

## 📁 Структура файлов проекта

```
GomokuGame.Trainer.MCP\
├── Program.cs                    # Входная точка CLI (265 строк) ✅
├── Configuration.cs              # Загрузка путей из config.json
├── Models/
│   └── TrainingState.cs          # Модели данных
├── LogAnalyzers/
│   ├── CsvLogParser.cs           # Парсер CSV с валидацией (197 строк)
│   ├── Exceptions.cs             # 3 типа исключений
│   ├── CliAnalyzer.cs            # Анализ для CLI (94 строк)
│   ├── TestRunner.cs             # Центральный контроллер тестов
│   └── InteractiveMode.cs        # Интерактивный режим
├── Commands/
│   ├── ITrainCommand.cs          # Интерфейс команды train ✅
│   └── TrainCommand.cs           # Реализация команды train (190 строк) ✅
├── Services/
│   ├── ITrainerController.cs     # Интерфейс управления обучением
│   └── TrainerController.cs      # Контроллер обучения (307 строк)
├── tests/                        # Тестовые файлы изолированы
│   ├── baseline_d_20000_permuted.txt
│   ├── baseline_d_missing_missed.txt
│   ├── baseline_d_extra_field.txt
│   └── baseline_d_invalid_value.txt
├── logs/                         # Папка для реальных логов (read-only)
├── config.json                  # Конфигурация окружения
└── GomokuGame.Trainer.MCP.csproj
```

---

## 🔍 Функциональность CsvLogParser

### Поддерживаемые сценарии валидации:

1. ✅ **Перестановка колонок** — парсинг независим от порядка колонок (header-based)
2. ✅ **Отсутствие обязательной колонки** — `MissingFieldException`
3. ✅ **Лишнее поле в строке** — `FieldCountMismatchException`
4. ✅ **Неверное числовое значение** — `InvalidFieldException`

### Пример анализа лога:

```
=== Анализ лога: tests\baseline_d_20000_permuted.txt ===

--- Первая запись (warmup batch) ---
Done:   500 партий
X:      303 побед за себя
O:      197 побед противника
Draw:   0 ничьих
Loss:   0,0792
Missed: 1 378 пропущенных побед
WinRate: 60,60% ((X*100)/Done)

--- Сводная статистика ---
Всего партий: 500
Победы X:     303
Победы O:     197
Ничьих:       0
Средний Loss: 0,0792
Средние Missed Wins: 1 378
```

---

## ⚠️ Важные ограничения

### 🚫 Не трогать эталонные сети:

Не запускайте обучение с названиями:
- `baseline_a_20000.json` → `baseline_e_20000.json`
- `grade_main_check_d_20000.json`
- `topology_*.json`

Эти файлы содержат эталонные веса обученных сетей для сравнения!

### 📝 CSV логи — read-only:

Проект только **анализирует** CSV-логи обучения, не перезаписывает их.

---

## 🧪 Запуск тестов

```bash
dotnet run --test

=== Тестирование CsvLogParser ===

--- Тест 1: Перестановка колонок ---
✓ Парсинг прошёл успешно!
✓ Перестановка колонок обработана корректно!

--- Тест 2: Отсутствие обязательной колонки 'Missed' ---
✓ ✓ ✓ Механизм работы:
  Тип исключения: MissingFieldException
  Сообщение: Отсутствует обязательная колонка: Missed
✓ Отсутствующая колонка 'Missed' корректно обнаружена!

--- Тест 3: Наличие лишнего поля 'ExtraColumn' ---
✓ ✓ ✓ Механизм работы:
  Тип исключения: FieldCountMismatchException
  Сообщение: Строка данных содержит 7 значений, но ожидается 6
✓ Проверка количества полей работает корректно!

--- Тест 4: Неверное числовое значение в поле 'Missed' ---
✓ ✓ ✓ Механизм работы:
  Тип исключения: InvalidFieldException
  Сообщение: Строка 2, поле 'Missed' имеет неверный формат
✓ Проверка формата числового значения работает корректно!

✓ Все тесты завершены!
```

---

## 🛠️ Архитектура

### Header-based Parsing

CSV-логи читаются по **названиям колонок**, а не по позициям. Это позволяет:
- Обрабатывать логи с переставленными колонками
- Гарантировать корректный парсинг независимо от формата
- Обеспечить обратную совместимость при изменениях в логах

### Exception-based Error Reporting

Все ошибки парсинга возвращаются как исключения:
- `MissingFieldException` — отсутствует обязательная колонка
- `FieldCountMismatchException` — несовпадение количества полей
- `InvalidFieldException` — неверный формат числового значения

### Command Pattern для запуска обучения

Новая архитектура запуска обучения через интерфейс `ITrainCommand`:
- Интерфейс `ITrainCommand` определяет контракт команды
- Реализация `TrainCommand` управляет запуском процесса через ShellExecute
- Поддерживает все параметры командной строки приложения GomokuGame.Trainer.exe (key:value)

### Full Parameter Support for Trainer CLI

Команда `train` поддерживает полный набор параметров тренера:
```bash
dotnet run train D 20000 --games:50000 --lr:0.01 --epsilon:0.3 --seed:42 \
  --save:network_A --algorithm:standard --mode:C --correction:FULL \
  --chunk:500 --warmup:500 --snapshotEvery:100 --demo:1 --board:9 --win:5
```

---

## 📦 Зависимости

Проект использует следующие зависимости (указаны в `.csproj`):
- `Serilog.Sinks.Console` — консольный вывод
- `Serilog.Sinks.File` — вывод в файл

---

## 📝 Примечания

- **Не меняйте** файлы в основном проекте GomokuGame
- **CSV логи** только читаются (read-only доступ)
- **Эталонные сети** не должны быть перезаписаны
- Проект предназначен для **запуска и анализа** обучения Gomoku

---

## 📞 Поддерживаемые команды

| Команда | Описание | Параметры |
|---------|----------|-----------|
| `dotnet run train <сеть> <партии> [saveName] [seed] [learningRate]` | Запуск обучения ✅ | A/B/C/D/E, кол-во партий (по умолчанию 20000) |
| `dotnet run analyze <лог>` | Анализ CSV-лога | путь к лог |
| `dotnet run start <сеть> [партии]` | Запуск обучения (устарело) | A/B/C/D/E, кол-во партий |
| `dotnet run stop` | Остановить обучение | - |
| `dotnet run status` | Проверить статус | - |
| `dotnet run help` | Показать справку | - |
| `dotnet run --test` | Запустить тесты | - |

---

**Версия:** 1.2.0  
**Дата создания:** 2026-11-01  
**Статус:** ✅ Работающий MCP-сервер для анализа и запуска обучения Gomoku с полной поддержкой параметров тренера
<EOF>