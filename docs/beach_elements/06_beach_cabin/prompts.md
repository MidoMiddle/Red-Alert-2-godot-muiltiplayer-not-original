# 06. Пляжная кабинка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пляжная кабинка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели кабинки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `beach_cabin` | здание | 1x1 | 2,2 | ≤ 2 000 | `body`, `door` | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a small wooden beach changing cabin in bright summer daylight: light blue painted walls, a single-pitch roof, a door with a handle, a small window. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_cabin`

```text
A small wooden beach changing cabin with light blue painted walls, a single-pitch grey roof, a door with a handle and a small window, about two and a fifth units tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `body` (стены и крыша), `door` (дверь, может открываться в коде).
- Гарнизона нет: внутри не занимают бойцов.
- Покраска — текстура, не геометрия.
- Бюджет ≤ 2 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_cabin.glb` | | | |

## Открытые вопросы

1. Открыта ли дверь по умолчанию (вход для отдыхающих) или закрыта.
