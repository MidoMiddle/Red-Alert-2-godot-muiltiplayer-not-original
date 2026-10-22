# 05. Мосты: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Мосты](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей пролёта. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Цвет команды |
|---|---|---|---|---|---|
| `bridge_span` | модель | 1x1 | 0,5 | ≤ 3 000 | нет |
| `bridge_span_damaged` | модель | 1x1 | 0,5 | ≤ 3 000 | нет |

Концевые секции (съезды на берег) и разрушенные варианты концов делаем в Blender из `bridge_span`, отдельно не генерируем. Высокие мосты — M11, сейчас не делаем.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/bridge/`.

```text
Concept art reference for a low bridge over a frozen river in winter: concrete piers standing in the ice, a wooden and concrete deck covered with snow, simple metal railings with frost. Three-quarter view from 45 degrees, looking down at about 30 degrees. Soft cold light, realistic materials, square image, no people, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `bridge_span`

Промт:

```text
A single straight bridge span one unit long and one unit wide: a concrete deck with wooden planks and snow on top, low metal railings with frost, two concrete piers at the ends standing in a frozen river. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `bridge_span_damaged`

Промт:

```text
A single bridge span one unit long, broken in the middle: a section of the deck is missing, twisted metal beams stick out, one concrete pier is cracked, broken planks and debris float in the frozen river, snow around the breaks. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пролёт идёт вдоль оси X. Поворот под направление моста делает код при размещении.
- Пивот — центр клетки, низ опор на уровне воды. Так пролёт стоит на реке, а не висит над ней.
- Настил чуть выше уровня берега, это важно для проходимости (M7, 4.6): проезд по пролёту — как по дороге.
- Разрушенный пролёт — отдельная модель. Поэтому пустой проём видно сразу и не путаем его с целым.
- Цвет команды не нужен: мост нейтральный.
- Материалов не больше двух: дерево и бетон.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `bridge_span.glb` | | | |
| `bridge_span_damaged.glb` | | | |
| `bridge_start`, `bridge_end`, `_damaged` (из Blender) | | | |

## Открытые вопросы

1. Нужна ли отдельная модель будки ремонта моста (она в [12_tech_buildings](../12_tech_buildings/prompts.md)).
2. Высота настила над водой: ориентир 0,15 клетки, проверим в визуальной пробе.
