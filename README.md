# Delphi Dependency Analyzer

CLI-утилита для анализа зависимостей Delphi проектов в монорепозиториях.

## Описание

Утилита анализирует `.dproj` файлы, парсит исходники Delphi (`.pas`, `.dpr`) и строит полный граф транзитивных зависимостей, включая:
- Все `.pas` юниты проекта
- `uses` зависимости (interface и implementation секции)
- `.inc` файлы (директивы `{$I}` и `{$INCLUDE}`)
- Граф зависимостей между юнитами

## Статус разработки

**Phase 1 (MVP)** — ✅ **Завершено**
**Phase 2 (Optimization)** — ✅ **Завершено**

### Phase 1: Базовая функциональность
- ✅ Парсинг `.dproj` файлов
- ✅ Извлечение search paths и главного `.dpr` файла
- ✅ Парсинг Delphi исходников (`.pas`, `.dpr`)
- ✅ Извлечение `uses` секций
- ✅ Парсинг `{$I}` и `{$INCLUDE}` директив
- ✅ Удаление комментариев (`//`, `{ }`, `(* *)`)
- ✅ Разрешение путей к файлам (5 уровней вниз/вверх)
- ✅ Рекурсивное построение графа зависимостей
- ✅ Детекция циклических зависимостей
- ✅ Генерация JSON с результатами
- ✅ CLI интерфейс

### Phase 2: Оптимизация производительности
- ✅ Система кэширования с SHA256 хэшированием
- ✅ Параллельный парсинг файлов (Parallel.ForEach)
- ✅ Метрики производительности (--performance флаг)
- ✅ Потокобезопасные коллекции (ConcurrentDictionary)
- ✅ Incremental анализ (только измененные файлы перепарсятся)

## Требования

- .NET 10.0 или выше

## Сборка

```bash
cd DelphiDepsAnalyzer
dotnet build
```

## Использование

Утилита поддерживает две команды:
1. **Анализ зависимостей** (по умолчанию) — строит граф зависимостей и генерирует JSON
2. **list-files** — извлекает список всех файлов проекта с относительными путями

### Базовый анализ проекта

```bash
dotnet run -- path/to/project.dproj
```

### С метриками производительности

```bash
dotnet run -- path/to/project.dproj --performance
# или
dotnet run -- path/to/project.dproj -p
```

### Команда list-files

Извлекает список всех файлов проекта (`.pas`, `.dpr`, `.inc`) с путями относительно корня репозитория. Полезно для CI/CD интеграции.

```bash
# Вывод списка файлов в консоль
dotnet run -- list-files path/to/project.dproj

# Сохранение списка в файл
dotnet run -- list-files path/to/project.dproj --output files.txt

# Указание корня репозитория вручную
dotnet run -- list-files path/to/project.dproj --repo-root /path/to/monorepo

# Комбинация опций
dotnet run -- list-files path/to/project.dproj --repo-root /path/to/monorepo --output files.txt
```

**Опции команды list-files:**
- `--repo-root <путь>` — корень репозитория (если не указан, ищется `.git` автоматически)
- `--output <путь>` — файл для сохранения результата (если не указан, вывод в консоль)

**Использование в CI:**
```bash
# Получить список файлов проекта
./DelphiDepsAnalyzer.exe list-files MyProject.dproj --output project-files.txt

# Проверить, какие изменённые файлы относятся к проекту
git diff --name-only origin/main | grep -f project-files.txt
```

### Примеры

```bash
# Базовый анализ
dotnet run -- ../TestDelphiProject/TestProject.dproj

# С детальными метриками
dotnet run -- ../TestDelphiProject/TestProject.dproj --performance

# Извлечение списка файлов
dotnet run -- list-files ../TestDelphiProject/TestProject.dproj
```

### Вывод

Утилита выводит:
1. Процесс анализа с детальными логами
2. Сводку с количеством юнитов и зависимостей
3. Список всех юнитов проекта
4. Список include файлов

### Результаты

Создаётся файл `<проект>.deps.json` рядом с `.dproj` файлом.

Структура JSON:
```json
{
  "project": {
    "path": "путь к .dproj",
    "mainSource": "путь к .dpr",
    "searchPaths": ["..."]
  },
  "units": [
    {
      "name": "UnitName",
      "path": "путь к .pas",
      "dependencies": {
        "interface": ["System.SysUtils", "OtherUnit"],
        "implementation": []
      },
      "includes": ["constants.inc"]
    }
  ],
  "includes": ["полные пути к .inc файлам"],
  "graph": {
    "nodes": [{"unit": "name", "path": "path"}],
    "edges": [{"from": "unit1", "to": "unit2"}]
  },
  "statistics": {
    "totalUnits": 4,
    "totalIncludes": 1,
    "totalDependencies": 8,
    "analyzedAt": "2026-02-04T09:29:44Z"
  }
}
```

## Публикация single-file executable

```bash
cd DelphiDepsAnalyzer
dotnet publish -c Release -r win-x64 --self-contained
```

Executable будет в `bin/Release/net10.0/win-x64/publish/DelphiDepsAnalyzer.exe`

## Архитектура

Проект состоит из следующих компонентов:

### Models
- `DelphiProject` — модель проекта Delphi
- `DelphiUnit` — модель юнита (.pas/.dpr)
- `DependencyGraph` — граф зависимостей

### Parsers
- `DelphiProjectParser` — парсит `.dproj` (XML)
- `DelphiSourceParser` — парсит `.pas` и `.dpr` файлы, обрабатывает условные директивы компиляции

### Core
- `PathResolver` — разрешает пути к файлам юнитов (5 уровней поиска вверх/вниз)
- `DependencyAnalyzer` — строит граф зависимостей с параллельной обработкой
- `DependencyCache` — кэширование с SHA256 хэшированием
- `PerformanceMetrics` — сбор метрик производительности
- `RepositoryRootFinder` — определяет корень репозитория (поиск `.git`)

### Output
- `JsonOutputFormatter` — генерирует JSON и выводит сводку
- `RelativePathOutputFormatter` — форматирует список файлов для команды `list-files`

## Особенности парсинга

### Uses секции
```pascal
uses
  System.SysUtils,      // системный юнит
  UnitA,                // пользовательский юнит
  UnitB in 'path.pas',  // юнит с явным путём
  Vcl.Forms;            // qualified name
```

### Include директивы
```pascal
{$I constants.inc}
{$INCLUDE 'utils.inc'}
```

### Обработка комментариев
- `//` — однострочные комментарии
- `{ }` — блочные комментарии
- `(* *)` — альтернативные блочные комментарии
- `{$...}` — директивы компилятора (не удаляются)

### Условные директивы компиляции
Парсер обрабатывает условные директивы в uses секциях, удаляя их и объединяя все варианты:

```pascal
uses
  {$IFNDEF LINUX}
    Vcl.Controls, Vcl.ActnList, Vcl.Forms,
  {$ENDIF}
  {$IFDEF LINUX}
    FMX.Controls, FMX.Forms,
  {$ENDIF}
  SysUtils;
```

Результат: `Vcl.Controls`, `Vcl.ActnList`, `Vcl.Forms`, `FMX.Controls`, `FMX.Forms`, `SysUtils`

Поддерживаемые директивы: `{$IFDEF}`, `{$IFNDEF}`, `{$ENDIF}`, `{$ELSE}`, `{$ELSEIF}`, `{$IFOPT}`

## Особенности поиска файлов

### Глубина поиска
- **Вниз**: до 5 уровней вложенности в поддиректориях
- **Вверх**: до 5 уровней вверх в родительских директориях

Это позволяет находить файлы, которые:
- Находятся в глубоко вложенных папках проекта
- Расположены выше проекта в структуре монорепозитория

### Пример структуры
```
MonoRepo/
├── SharedLibs/
│   └── CommonUnit.pas      ← Найдётся (2 уровня вверх)
├── ProjectA/
│   ├── ProjectA.dproj
│   └── Modules/
│       └── Deep/
│           └── Level3/
│               └── DeepUnit.pas  ← Найдётся (3 уровня вниз)
```

## Известные ограничения

1. Не поддерживаются макросы в путях (`$(Platform)`, `$(Config)`)
2. Не поддерживаются `.dpk` пакеты
3. Условные директивы компиляции удаляются из uses секций (все варианты объединяются)

**Примечание:** Условные директивы (`{$IFDEF}`, `{$IFNDEF}`, `{$ENDIF}`) в uses секциях обрабатываются путём удаления директив и объединения всех вариантов юнитов. Например:
```pascal
uses
  {$IFDEF WINDOWS}
    Vcl.Forms,
  {$ELSE}
    FMX.Forms,
  {$ENDIF}
  SysUtils;
```
Парсер извлечёт: `Vcl.Forms`, `FMX.Forms`, `SysUtils` (все варианты для всех платформ).

Эти ограничения будут устранены в Phase 3.

## Roadmap

### Phase 2 — Optimization ✅ **Завершено**
- ✅ Кэширование результатов анализа (SHA256)
- ✅ Параллельный парсинг файлов
- ✅ Incremental анализ
- ✅ Профилирование производительности

### Phase 3 — Production (планируется)
- Поддержка `--changed-files`
- CI/CD интеграция
- Поддержка условной компиляции
- Поддержка `.dpk` пакетов
- Улучшенное логирование
- Unit тесты
- Обработка edge cases

## Пример вывода

### Базовый запуск
```
Delphi Dependency Analyzer v1.1 (Phase 2)
============================================================

1. Парсинг проекта: TestProject.dproj

2. Анализ зависимостей:
  [CACHE HIT] Главный файл загружен из кэша
  [OK] Найден: UnitA -> D:\Projects\TestDelphiProject\UnitA.pas
  [CACHE HIT] Используем закэшированный: UnitA
  [OK] Найден: UnitB -> D:\Projects\TestDelphiProject\UnitB.pas
  [CACHE HIT] Используем закэшированный: UnitB
  [SKIP] Системный юнит: System.SysUtils

Анализ завершён:
  Найдено юнитов: 4
  Найдено include файлов: 1
  Рёбер в графе: 8

============================================================
СВОДКА АНАЛИЗА ЗАВИСИМОСТЕЙ
============================================================
Проект:           TestProject.dproj
Главный файл:     TestProject.dpr
Всего юнитов:     4
Include файлов:   1
Зависимостей:     8
============================================================

✓ Анализ успешно завершён!
Общее время: 0.09с
```

### С флагом --performance
```
...

============================================================
МЕТРИКИ ПРОИЗВОДИТЕЛЬНОСТИ
============================================================
Общее время:      0.08с

Операции:
  Parse .dproj                            15.5мс  (  1x, avg   15.5мс,  18.6%)
  Save JSON                                9.4мс  (  1x, avg    9.4мс,  11.2%)
  Analyze Dependencies                     9.3мс  (  1x, avg    9.3мс,  11.2%)
  Save Cache                               4.7мс  (  1x, avg    4.7мс,   5.6%)
  Resolve Unit Path                        0.3мс  (  3x, avg    0.1мс,   0.3%)
============================================================

СТАТИСТИКА КЭША
============================================================
Записей в кэше:   4
Cache Hits:       4
Cache Misses:     0
Hit Rate:         100.0%
============================================================
```

### Команда list-files
```
Delphi Dependency Analyzer v1.2 (Phase 2)
============================================================

1. Парсинг проекта: TestProject.dproj

2. Анализ зависимостей:
  [CACHE HIT] Главный файл загружен из кэша
  [OK] Найден: UnitA -> D:\Projects\TestDelphiProject\UnitA.pas
  [CACHE HIT] Используем закэшированный: UnitA
  [OK] Найден: UnitB -> D:\Projects\TestDelphiProject\UnitB.pas
  [CACHE HIT] Используем закэшированный: UnitB

3. Определение корня репозитория:
Корень репозитория: D:\Projects

4. Формирование списка файлов:

TestDelphiProject/TestProject.dpr
TestDelphiProject/UnitA.pas
TestDelphiProject/UnitB.pas
TestDelphiProject/UnitC.pas
TestDelphiProject/constants.inc

✓ Команда list-files успешно выполнена!
Общее время: 0.05с
```

## Технологии

- C# 12
- .NET 10.0
- System.Xml.Linq (для XML парсинга)
- System.Text.Json (для JSON генерации)
- Регулярные выражения (для парсинга Delphi кода)
- System.Security.Cryptography (SHA256 хэширование)
- System.Collections.Concurrent (потокобезопасные коллекции)
- Parallel.ForEach (параллельная обработка зависимостей)

## Лицензия

MIT
