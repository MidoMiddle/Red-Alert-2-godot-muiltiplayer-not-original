# 01. Лиственное дерево: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Лиственное дерево](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей лиственного дерева. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Варианты |
|---|---|---|---|---|
| `tree_deciduous_01` … `_03` | 1x1 | 2 | ≤ 3 000 | три кроны |
| `tree_deciduous_stump` | 1x1 | 0,5 | ≤ 500 | один |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for broadleaf trees at the edge of a farm field in warm daylight: three oak-like trees with wide round green crowns and short thick trunks, a cut stump in the grass. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `tree_deciduous_01` (базовый промт)

```text
A broadleaf tree about two units tall: a wide round crown of green leaves, lighter on top and darker below, a short thick trunk with grey-brown bark at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `tree_deciduous_02`, `_03`

Взять промт 3.1 и добавить одну строку в конец:
- `_02`: `The crown is taller and narrower.`
- `_03`: `The crown is wider and lower, with a few bare branches.`

### 3.3. `tree_deciduous_stump`

```text
A cut broadleaf tree stump about half a unit tall, flat light cut surface with growth rings, bark at the edges, grass at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Листва — геометрия, не альфа-карты (как у елей, [07_trees](../../map_elements/07_trees/prompts.md)).
- Пивот — центр клетки на земле.
- Цвета команды нет.
- Бюджет ≤ 3 000 треугольников (LOD0), LOD1 около 50 %.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `tree_deciduous_01.glb` | | | |
| `tree_deciduous_02.glb` | | | |
| `tree_deciduous_03.glb` | | | |
| `tree_deciduous_stump.glb` | | | |

## Открытые вопросы

1. Осенняя палитра для листвы (см. [README](README.md)).
