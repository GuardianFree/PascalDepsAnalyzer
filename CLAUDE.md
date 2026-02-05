# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Обзор проекта

CLI-утилита для анализа зависимостей проектов Delphi (Object Pascal) в монорепозиториях.

**Основная задача**: Проанализировать .dproj файл и построить полный граф транзитивных зависимостей, включая .pas юниты, .inc файлы и uses-зависимости.

**Технологический стек**: C# 12, .NET 10.0, single-file publish (без внешних зависимостей).

**Статус**:
- ✅ **Phase 1 (MVP) завершён** — базовая функциональность реализована и протестирована
- ✅ **Phase 2 (Optimization) завершён** — кэширование, параллелизм и метрики производительности

## Команды для разработки

### Сборка и запуск
```bash
# Сборка проекта
cd DelphiDepsAnalyzer
dotnet build

# Запуск на тестовом проекте
dotnet run -- ../TestDelphiProject/TestProject.dproj

# Создание single-file executable (Release)
dotnet publish -c Release -r win-x64 --self-contained
# Результат: bin/Release/net10.0/win-x64/publish/DelphiDepsAnalyzer.exe
```

### Тестирование
```bash
# Запуск на тестовом проекте
cd DelphiDepsAnalyzer
dotnet run -- ../TestDelphiProject/TestProject.dproj

# С метриками производительности
dotnet run -- ../TestDelphiProject/TestProject.dproj --performance

# Проверка созданного JSON
cat ../TestDelphiProject/TestProject.deps.json

# Тестирование команды list-files
dotnet run -- list-files ../TestDelphiProject/TestProject.dproj

# Сохранение списка файлов
dotnet run -- list-files ../TestDelphiProject/TestProject.dproj --output files.txt
```

## Структура проекта

```
DelphiDepsAnalyzer/
├── Models/              # Модели данных
│   ├── DelphiProject.cs
│   ├── DelphiUnit.cs
│   └── DependencyGraph.cs
├── Parsers/             # Парсеры файлов
│   ├── DelphiProjectParser.cs
│   └── DelphiSourceParser.cs    # Парсинг uses и условных директив
├── Core/                # Основная логика
│   ├── PathResolver.cs           # Поиск файлов (5 уровней вверх/вниз)
│   ├── DependencyAnalyzer.cs     # Граф зависимостей с параллелизмом
│   ├── DependencyCache.cs        # Кэширование с SHA256
│   ├── PerformanceMetrics.cs     # Сбор метрик
│   └── RepositoryRootFinder.cs   # Поиск корня репозитория (.git)
├── Output/              # Форматирование результатов
│   ├── JsonOutputFormatter.cs           # Генерация JSON для анализа
│   └── RelativePathOutputFormatter.cs   # Вывод списка файлов (list-files)
└── Program.cs           # CLI интерфейс

TestDelphiProject/       # Тестовый проект Delphi
├── TestProject.dproj
├── TestProject.dpr
├── UnitA.pas
├── UnitB.pas
├── UnitC.pas
├── constants.inc
└── .deps-cache/         # Кэш анализа (создается автоматически)
    └── cache.json
```

## Контекст использования

Проект предназначен для работы с большими монорепозиториями Delphi, где:
- Десятки проектов с .dproj, .dpr, .pas, .inc файлами
- Юниты подключаются через `uses`
- Include-директивы через `{$I}` / `{$INCLUDE}`
- Search paths могут указывать на любые директории в монорепозитории
- Зависимости могут находиться где угодно в репозитории

## Архитектура

### Фазы разработки

#### Phase 1 — MVP
Минимально рабочий dependency extractor:
- Парсинг .dproj для извлечения пути к .dpr и search paths
- Анализ .dpr и .pas файлов для извлечения `uses` секций
- Рекурсивное построение списка зависимостей
- Генерация JSON файла с результатами (project.deps.json)

Без CI интеграции, кэширования и оптимизаций.

#### Phase 2 — Optimization ✅ **Завершён**
- ✅ Кэширование результатов анализа (SHA256 хэширование, .deps-cache/cache.json)
- ✅ Параллельный парсинг файлов (Parallel.ForEach, ConcurrentDictionary)
- ✅ Incremental анализ (перепарсинг только измененных файлов)
- ✅ Профилирование производительности (PerformanceMetrics, --performance флаг)
- ✅ **Оптимизация кэша (v1.2.1):**
  - Устранено двойное вычисление SHA256 (in-memory hash cache)
  - Быстрая проверка по LastModified + FileSize перед SHA256
  - Убрано избыточное логирование в параллельных потоках
  - Кэширование путей в PathResolver
  - **Результат: 2-й запуск на 20-50% быстрее первого**
- ✅ **Оптимизация PathResolver (v1.2.2):**
  - Предварительная индексация всех .pas файлов при инициализации
  - Замена рекурсивного поиска на O(1) lookup в индексе
  - Поддержка qualified names в индексе (Vcl.Forms)
  - Умная приоритизация при коллизиях имён в монорепозитории:
    1. Приоритет по порядку search paths
    2. Близость к корню проекта
    3. Меньшая глубина вложенности
  - **Результат: ускорение Resolve Unit Path в ~419,000 раз (335с → 0.7мс)**
  - **На больших проектах: с 20 минут до < 1 секунды**

#### Phase 3 — Production
- Поддержка changed files detection
- CI интеграция
- Rebuild detection
- Расширяемость архитектуры
- Стабильные форматы выходных данных
- Детальное логирование
- Покрытие тестами
- Обработка edge cases Delphi синтаксиса

### Ключевые компоненты

1. **ProjectParser** — парсинг .dproj (XML), извлечение путей и конфигурации
2. **DelphiParser** — парсинг .dpr и .pas файлов, извлечение `uses` и `{$I}`
3. **PathResolver** — разрешение путей к файлам на основе search paths
   - Предварительная индексация всех .pas файлов при инициализации
   - O(1) lookup по имени юнита в индексе
   - Поддержка qualified names (Vcl.Forms, System.SysUtils)
   - Кэширование результатов разрешения путей
   - Fallback на прямой поиск для динамически созданных файлов
4. **DependencyAnalyzer** — построение графа зависимостей
   - Parallel.ForEach для параллельной обработки
   - ConcurrentDictionary для потокобезопасности
   - Интеграция с DependencyCache
5. **DependencyCache** — кэширование результатов парсинга
   - SHA256 хэширование файлов для инвалидации
   - Сохранение в .deps-cache/cache.json
   - Статистика cache hits/misses
6. **PerformanceMetrics** — сбор метрик производительности
   - Измерение времени операций
   - Подсчет количества вызовов
   - Вывод детальных отчетов с --performance
7. **OutputFormatter** — генерация JSON с результатами

## Алгоритм анализа

1. Инициализировать кэш (загрузить из .deps-cache/cache.json если существует)
2. Прочитать .dproj, извлечь:
   - Путь к главному .dpr файлу
   - Search paths
   - Include paths
3. Парсить главный .dpr файл:
   - Проверить кэш (по SHA256 хэшу файла)
   - Если в кэше - использовать, иначе парсить
   - Извлечь `uses` секции (interface/implementation)
   - Извлечь `{$I}` / `{$INCLUDE}` директивы
4. Для каждого юнита из `uses` (параллельно с Parallel.ForEach):
   - Разрешить путь на основе search paths
   - Найти соответствующий .pas файл
   - Проверить кэш перед парсингом
   - Рекурсивно проанализировать его зависимости
5. Построить граф зависимостей (избегая циклов, с потокобезопасностью)
6. Сохранить результаты в JSON
7. Сохранить кэш на диск

## Парсинг Delphi

### Обработка uses секций
```pascal
uses
  System.SysUtils,  // стандартный юнит
  MyUnit,           // пользовательский юнит
  SubDir.MyUnit;    // юнит в подпапке
```

### Обработка includes
```pascal
{$I constants.inc}
{$INCLUDE 'utils.inc'}
```

### Edge cases
- Комментарии: `//`, `{ }`, `(* *)`
- Условная компиляция: `{$IFDEF}`, `{$IFNDEF}`, `{$ENDIF}`, `{$ELSE}`, `{$ELSEIF}`, `{$IFOPT}`
  - Директивы корректно удаляются из uses секций
  - Переносы строк внутри uses обрабатываются как разделители
- Строковые литералы с директивами внутри
- Qualified unit names (например, `Vcl.Forms`)

**Пример обработки условных директив:**
```pascal
uses
  {$IFNDEF LINUX}
    Vcl.Controls, Vcl.ActnList, Vcl.Forms, Vcl.Graphics, ShortCuts,
  {$ENDIF}
  {$IFDEF LINUX}
    FMX.Controls, FMX.Forms,
  {$ENDIF}
  SysUtils;
```
Парсер извлечёт: `Vcl.Controls`, `Vcl.ActnList`, `Vcl.Forms`, `Vcl.Graphics`, `ShortCuts`, `FMX.Controls`, `FMX.Forms`, `SysUtils`

## Использование

### Основная команда: анализ зависимостей

Выполняет полный анализ проекта и генерирует JSON файл с графом зависимостей.

```bash
# Базовый анализ проекта
analyzer.exe path/to/project.dproj

# С метриками производительности
analyzer.exe path/to/project.dproj --performance

# Вывод:
# - Список всех .pas юнитов
# - Список .inc файлов
# - Граф транзитивных зависимостей
# - JSON файл: project.deps.json
```

### Команда list-files: извлечение списка файлов

Выполняет анализ зависимостей и выводит список всех обнаруженных файлов с путями относительно корня репозитория. Полезно для интеграции с CI/CD системами и для определения набора файлов, затрагиваемых изменениями в проекте.

```bash
# Вывод списка файлов в консоль
analyzer.exe list-files path/to/project.dproj

# Сохранение списка файлов в файл
analyzer.exe list-files path/to/project.dproj --output files.txt

# Указание корня репозитория вручную
analyzer.exe list-files path/to/project.dproj --repo-root C:\Projects\MyMonorepo

# Комбинация опций
analyzer.exe list-files path/to/project.dproj --repo-root C:\Projects\MyMonorepo --output files.txt
```

**Опции команды list-files:**
- `--repo-root <путь>` — явно указать корень репозитория. Если не указан, утилита автоматически ищет папку `.git` в родительских директориях.
- `--output <путь>` — сохранить результат в файл. Если не указан, результат выводится в консоль.

**Формат вывода:**
Каждая строка содержит относительный путь к файлу от корня репозитория:
```
src/Units/UnitA.pas
src/Units/UnitB.pas
src/Common/constants.inc
TestProject.dpr
```

**Использование в CI:**
Команда полезна для определения списка файлов, которые нужно проверить в CI-pipeline:
```bash
# Получить список файлов проекта
analyzer.exe list-files MyProject.dproj --output project-files.txt

# Проверить, какие из изменённых файлов относятся к проекту
git diff --name-only origin/main | grep -f project-files.txt
```

## Формат выходных данных

Файл `project.deps.json`:
```json
{
  "project": "path/to/project.dproj",
  "mainSource": "path/to/project.dpr",
  "units": [
    {
      "path": "path/to/unit.pas",
      "dependencies": ["System.SysUtils", "MyOtherUnit"]
    }
  ],
  "includes": ["path/to/constants.inc"],
  "graph": {
    "nodes": [...],
    "edges": [...]
  }
}
```

## Требования к производительности

- Быстрая работа с большими проектами (сотни файлов)
- Offline-режим (без внешних зависимостей)
- Single-file executable для простоты распространения
- Минимальное потребление памяти
