# 07. Деревья и растительность: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Деревья и растительность](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей деревьев: четыре ели, пень и горелое дерево. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Варианты |
|---|---|---|---|---|
| `tree_spruce_01` … `tree_spruce_04` | 1x1 | 2 | ≤ 3 000 | четыре силуэта |
| `tree_stump` | 1x1 | 0,5 | ≤ 500 | один |
| `tree_spruce_burned` | 1x1 | 2 | ≤ 3 000 | один |

Четыре ели — по решению M7 (раздел «Объекты карты», 4.7). В M1 для пробы указано 2–3 вида; это не противоречит, M7 задаёт финальный набор.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/trees/`.

```text
Concept art reference for a snowy spruce forest edge at dusk: four conifer trees of different heights and shapes, a broken stump in the snow, and one burned black spruce. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `tree_spruce_01` (базовый промт для всех четырёх елей)

```text
A snow-covered spruce tree about two units tall, conical silhouette, dark green needles with white snow resting on the branch tiers, a thick brown trunk with realistic bark at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `tree_spruce_02`, `_03`, `_04`

Взять промт 3.1 и добавить одну строку в конец:

- `_02`: `Taller and slimmer than a typical spruce.`
- `_03`: `Wider and lower, with a few broken branches on one side.`
- `_04`: `With a broken top and a thinner crown.`

### 3.3. `tree_stump`

```text
A cut tree stump in the snow about half a unit tall, flat light-coloured cut surface with visible rings, bark at the edges, a little snow on top. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.4. `tree_spruce_burned`

```text
A burned spruce tree about two units tall, charred black trunk and bare blackened branches with no needles, snow patches at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Деревья — инстансинг. Случайный поворот и масштаб ±15 % делает код (M1, 4.5).
- Листва — геометрия, не альфа-карты: так проще в инстансинге и без сортировки прозрачности.
- Пень — отдельная модель. Падение дерева — анимация поворота в коде, отдельной модели не делаем.
- Горящее дерево — только модель. Пламя и дым — частицы в Godot.
- Цвет команды не нужен.
- Низкая генерация важнее красоты: густая крона у генераторов обычно сильно перегружена.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `tree_spruce_01.glb` | | | |
| `tree_spruce_02.glb` | | | |
| `tree_spruce_03.glb` | | | |
| `tree_spruce_04.glb` | | | |
| `tree_stump.glb` | | | |
| `tree_spruce_burned.glb` | | | |

## Открытые вопросы

1. Полупрозрачные кроны над юнитами, чтобы их было видно внутри леса.
2. Распространяется ли огонь с дерева на дерево (лесной пожар). Это влияет на то, нужна ли отдельная модель для стадии «тлеет».
