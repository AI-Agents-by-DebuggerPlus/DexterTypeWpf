# AGENTS.md — служебная карточка проекта DexterType

> **Правило проекта.** В корне каждого проекта обязателен этот файл (`AGENTS.md`).
> Любой ИИ-агент **перед началом работы** читает его, а после значимых изменений —
> **обновляет**. Здесь: какой агент ведёт проект, на каком этапе он находится,
> что делать дальше и на какие файлы опираться. Аналог правил `.cursor/rules` в Cursor.

## 1. Кто ведёт проект

- **Агент:** Hermes-профиль `sysadmin` (@npch_sysadmin_bot) — ведёт проект (уроки,
  версия сборки, развёртывание v2.0 из архива). Мост к ПК (Tailscale) — ресурс
  профиля `app_dev`.
- **Владелец:** Tony Stark (git-автор `Sergey Deleu <businessdeleu@gmail.com>`).
- **Репозиторий (рабочая папка):** `d:\Programming\AI_Agents\WPF\DexterTypeWpf\DexterType-WPF`
- **GitHub:** `https://github.com/AI-Agents-by-DebuggerPlus/DexterTypeWpf` (ветка `master`).

## 2. Текущий этап

**v2.0.0 — внедрено и собирается.** Полная переработка: ядро `Dexter.Core` (без WPF) +
WPF-приложение `DexterType` + генератор курсов `tools/generate_courses.py` +
самопроверка `tools/SelfCheck`.

Статус на момент записи:
- `dotnet build DexterType.slnx` — **успешно**, 0 ошибок (SDK 10.0.402, target net8.0-windows);
- `dotnet run --project tools\SelfCheck` — **«Все проверки пройдены»**
  (2 курса: 26 русских + 24 английских урока, 295 упражнений);
- exe: `DexterType\bin\Debug\net8.0-windows\DexterType.exe`, курсы копируются рядом.

## 3. Ближайшие задачи (по приоритету)

1. **Ручной прогон окон WPF** — пройти главное окно, настройки (F2), статистику (F7),
   сверить вёрстку/поведение со списком в `PROMPT.md` (код собирался, но глазами не проверялся).
2. **Руки на экранной клавиатуре** (как в Stamina): векторные кисти левой/правой руки,
   подсветка нужного пальца (`KeyDef.Finger`, `KeyHit.ShiftKey`).
3. **Метроном** из методики слогового метода (в приложении пока нет — внешний).

## 4. Опорные файлы (читать при доработке)

- **`PROMPT.md`** — полный промпт для ИИ-агента: цели, границы слоёв, правила работы, задачи.
- **`README.md`** — что внутри, установка, управление, звуки, формат курсов.
- **`docs/COURSE_FORMAT.md`** — формат JSON-курса (не менять `id` уроков — теряется прогресс).
- **Ядро (логика без UI):** `Dexter.Core/` — `Courses/CourseLoader.cs`, `Keyboard/KeyboardLayoutDef.cs`,
  `Typing/TypingSession.cs`, `Storage/*`, `Audio/SoundSynth.cs`.
- **UI:** `DexterType/MainWindow.xaml(.cs)`, `SettingsWindow.*`, `StatsWindow.*`,
  `Ui/KeyboardView.cs`, `Theming/Themes.cs`, `Services/SoundService.cs`, `Services/MusicService.cs`.
- **Контент курсов:** `DexterType/Courses/ru-basic.json`, `en-basic.json` (генерируются из
  `tools/courses/*.txt` через `tools/generate_courses.py`; JSON руками не править).
- **Проверка:** `tools/SelfCheck` — должна проходить всегда.

## 5. Правила ведения (кратко)

- Перед коммитом: `dotnet build DexterType.slnx` + `dotnet run --project tools\SelfCheck`.
- Границу слоёв не ломать: никаких WPF-типов в `Dexter.Core`.
- Язык интерфейса и комментариев — русский. C# 12, `Nullable` включён, без лишних NuGet.
- Если что-то не проверено (например, нет Windows) — прямо сказать об этом.
- Обновлять разделы 2–3 этого файла после значимых изменений.
