# 02. Пальма: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пальма](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей пальм. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Варианты |
|---|---|---|---|---|
| `tree_palm_01` … `tree_palm_03` | 1x1 | 3 | ≤ 3 000 | три изгиба ствола |
| `tree_palm_stump` | 1x1 | 0,5 | ≤ 500 | один |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for palm trees on a sandy beach in bright summer daylight: three palms with slightly curved trunks and wide drooping fronds, one short palm stump in the sand. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `tree_palm_01` (базовый промт для трёх пальм)

```text
A tall palm tree about three units tall with a slightly curved grey-brown trunk with dark rings, and a crown of wide drooping pinnate fronds. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `tree_palm_02`, `tree_palm_03`

Взять промт 3.1 и добавить одну строку в конец:

- `_02`: `The trunk curves strongly to one side.`
- `_03`: `The trunk is straighter and the crown is fuller.`

### 3.3. `tree_palm_stump`

```text
A short palm stump in the sand about half a unit tall, cut surface with a ring pattern, a few dry fronds at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Листья — геометрия, не альфа-карты, как у елей ([07_trees](../../map_elements/07_trees/prompts.md)).
- Пивот — центр клетки на земле.
- Цвет команды не нужен.
- Бюджет как у деревьев: ≤ 3 000 треугольников (LOD0), LOD1 около 50 %.
- Генераторы перегружают листву: упрощаем в Blender до бюджета.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `tree_palm_01.glb` | | | |
| `tree_palm_02.glb` | | | |
| `tree_palm_03.glb` | | | |
| `tree_palm_stump.glb` | | | |

## Открытые вопросы

1. Нужна ли отдельная модель горящей пальмы или достаточно шейдера обугливания.
