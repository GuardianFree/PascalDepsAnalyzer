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
│   └── DelphiSourceParser.cs
├── Core/                # Основная логика
│   ├── PathResolver.cs           # Поиск файлов (5 уровней вверх/вниз)
│   ├── DependencyAnalyzer.cs     # Граф зависимостей с параллелизмом
│   ├── DependencyCache.cs        # Кэширование с SHA256
│   └── PerformanceMetrics.cs     # Сбор метрик
├── Output/              # Форматирование результатов
│   └── JsonOutputFormatter.cs
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
   - Поиск вниз: до 5 уровней вложенности
   - Поиск вверх: до 5 уровней в родительских директориях
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
- Условная компиляция: `{$IFDEF}`, `{$IFNDEF}`, `{$ENDIF}`
- Строковые литералы с директивами внутри
- Qualified unit names (например, `Vcl.Forms`)

## Использование

```bash
# Базовый анализ проекта
analyzer.exe path/to/project.dproj

# Вывод:
# - Список всех .pas юнитов
# - Список .inc файлов
# - Граф транзитивных зависимостей
# - JSON файл: project.deps.json
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
