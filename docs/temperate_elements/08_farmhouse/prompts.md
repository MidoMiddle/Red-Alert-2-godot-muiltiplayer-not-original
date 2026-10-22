# 08. Фермерский дом: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Фермерский дом](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели дома. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `temperate_farmhouse` | здание | 2x2 | 2,5 | ≤ 8 000 | да, 3 | да (полоса над верандой) |

Повреждённый вариант — тот же промт с правкой «damaged» (как у городских домов, [01_small_house](../../city_elements/01_small_house/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a wooden farmhouse in warm daylight: a two-storey house with a pitched dark green roof, a wooden veranda with railings, white window frames with blue shutters, a small garden in front. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `temperate_farmhouse`

```text
A wooden farmhouse about two and a half units tall: pale whitewashed timber walls, a pitched dark green shingle roof, a wooden veranda with railings across the front, white window frames with blue shutters, and a narrow plain colour band above the veranda. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body`, `roof`, `team_mask` (полоса над верандой).
- Гарнизон 3: бойцы в окнах — код, в модели окна видны.
- Бюджет ≤ 8 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `temperate_farmhouse.glb` | | | |
| `temperate_farmhouse_damaged.glb` | | | |

## Открытые вопросы

1. Вместимость 3 или 2 (см. [README](README.md)).
