# 08. Заборы и нейтральные стены: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Заборы и нейтральные стены](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и секций заборов и стен. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 |
|---|---|---|---|
| `fence_wood_section` | 1x1 (одна секция) | 0,25 | ≤ 500 |
| `fence_mesh_section` | 1x1 (одна секция) | 0,5 | ≤ 500 |
| `wall_stone_section` | 1x1 (одна секция) | 0,6 | ≤ 500 |

Все секции — длиной ровно одна клетка. Углы, концы и повороты делаем в Blender из той же секции. Отдельно их не генерируем.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/fences/`.

```text
Concept art reference for a snowy yard edge: a wooden picket fence with snow on the top rail, a steel wire mesh fence on metal posts with frost, and a grey stone wall with a snow cap, all in one row. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `fence_wood_section`

```text
A straight section of a wooden picket fence, one unit long and a quarter unit tall, weathered grey planks, snow on the top rail, two posts at the ends. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `fence_mesh_section`

```text
A straight section of a steel wire mesh fence on two metal posts, one unit long and half a unit tall, mesh with frost, simple rust on the posts. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Сетка у генераторов получается тяжёлой: если не укладывается в бюджет, упрощаем сетку в Blender или делаем сетку текстурой с альфа-каналом.

### 3.3. `wall_stone_section`

```text
A straight section of a rough grey stone wall, one unit long and six tenths of a unit tall, stacked stones, a thin snow cap on top. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Высота секций задаёт видимость: пехота ≈ 0,5 клетки (см. [требования](../generation_requirements.md), раздел 2). Забор ниже пояса пехоты, стена выше неё, поэтому бойцы за стеной видны только по верхней части.
- Пивот — центр секции на земле. Секция стыкуется с соседними по краю клетки.
- Каменная стена — единственная секция с полным блокированием видимости. Остальные только физически.
- Повреждённый вариант (сломанные доски) — по желанию, делаем в Blender из той же секции. Давку гусеницами показывает код.
- Цвет команды не нужен.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `fence_wood_section.glb` | | | |
| `fence_mesh_section.glb` | | | |
| `wall_stone_section.glb` | | | |

## Открытые вопросы

1. Высота каменной стены: 0,6 клетки сейчас. В [appearance.md](appearance.md) написано «выше пехоты». Это согласуется только при росте пехоты около 0,5 клетки. Подтвердить рост пехоты в M1 или поправить описание.
2. Нужны ли мешки с песком отдельной моделью, которая выглядит как укрытие (в игре укрытий нет).
