# 14. Пирс: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пирс](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели сегмента пирса. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `pier_segment` | сегмент | 1x1 | 0,5 | ≤ 3 000 | `deck`, `posts` | флот — да, суша — по настилу нет |
| `pier_segment_damaged` | сегмент | 1x1 | 0,5 | ≤ 3 000 | `deck`, `posts` | то же |

Концевые сегменты (начало у берега и конец) делаем в Blender из `pier_segment`, как у моста ([05_bridges](../../map_elements/05_bridges/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a wooden pier segment going out into the sea in bright summer daylight: a weathered plank deck on dark wooden posts, low railings, water with small waves around the posts. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `pier_segment`

```text
A single straight wooden pier segment one unit long and one unit wide: a deck of weathered light planks with low railings, supported by dark wooden posts that stand in water, the deck about half a unit above the water. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Направление настила — по оси X, поворот — при размещении.

### 3.2. `pier_segment_damaged`

```text
A single wooden pier segment one unit long, broken in the middle: a section of the deck is missing, broken planks hang down, one post is cracked, debris floats in the water. Realistic physically based materials, clean readable silhouette, game-ready asset, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки, низ опор на уровне воды. Узлы: `deck` (настил), `posts` (опоры).
- Логика — как у моста: настил проходим для суши, блокирует флот (M7, 4.6).
- Цвета команды нет.
- Бюджет ≤ 3 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `pier_segment.glb` | | | |
| `pier_segment_damaged.glb` | | | |

## Открытые вопросы

1. Швартовка кораблей у пирса (см. [README](README.md)).
