# 07. Глиняный дом: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Глиняный дом](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели глиняного дома. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `desert_adobe_house` | здание | 2x2 | 2,2 | ≤ 8 000 | да, 2 | да (полоса на парапете) |

Повреждённый вариант — тот же промт с правкой «damaged» (как у [фермерского дома](../../temperate_elements/08_farmhouse/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for a mud-brick desert house in harsh bright sunlight: sand-coloured walls with soft edges, a flat roof with a low parapet, small windows with dark wooden grilles, a wooden door. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `desert_adobe_house`

```text
A small mud-brick desert house about two units tall: sand-coloured walls with soft rounded edges, a flat roof with a low parapet, small windows with dark wooden grilles, a wooden door, and a narrow plain colour band on the parapet. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body`, `roof` (плоская крыша с парапетом), `team_mask` (полоса на парапете).
- Гарнизон 2: бойцы в окнах — код.
- Бюджет ≤ 8 000 треугольников, текстуры 1024².
- Глина — текстура с потёками, без геометрии каждой трещины.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `desert_adobe_house.glb` | | | |
| `desert_adobe_house_damaged.glb` | | | |

## Открытые вопросы

1. Вместимость 2 или 3 (см. [README](README.md)).
