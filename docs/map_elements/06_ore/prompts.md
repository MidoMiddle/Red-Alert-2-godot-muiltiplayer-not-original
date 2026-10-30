# 06. Руда и самоцветы: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Руда и самоцветы](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей руды, самоцветов и рудного источника. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Цвет команды |
|---|---|---|---|---|---|
| `ore_cluster` | модель | 1x1 | 0,5 | ≤ 1 000 | нет |
| `gem_cluster` | модель | 1x1 | 0,5 | ≤ 1 000 | нет |
| `ore_source` | модель | 1x1 | 1,8 | ≤ 3 000 | нет |

Плотность руды делает код: на каждой клетке один кластер, он тем больше, чем выше стадия, и повёрнут по клетке (`game/scripts/Terrain/OreField.cs`). Поэтому кластер — не отдельный образец, а россыпь мелких кусков по всей клетке и чуть за её край, без подложки: кластеры соседних клеток сливаются в одно поле. Цвета самоцветов запечены в текстуру одной модели: каждая группа кристаллов своего цвета.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/ore/`.

```text
Concept art reference for a resource field in winter: a cluster of golden-yellow ore nuggets and pebbles lying on snow, a few blue gemstone crystals nearby, and one steel drilling tower with a rotating drill head as a resource source, with a few golden nuggets thrown up around its base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `ore_cluster`

```text
A cluster of golden-yellow ore nuggets and pebbles lying on a small patch of snowy ground, rounded lumpy stones of different sizes, semi-glossy gold surface with thin gold veins, a little dark soil at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `gem_cluster`

```text
A cluster of blue faceted gemstone crystals of different sizes, glossy, growing out of a small patch of snowy ground. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Цвета — в текстуре одной модели, см. раздел 1.

### 3.3. `ore_source`

```text
A steel lattice drilling tower about one unit wide and three units tall, standing on a small concrete base in the snow, with a rotating drill head at the top and a few golden nuggets around its base. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Узлы `ore_source`: `tower` (вышка), `drill` (бур; вращается в Godot по вертикальной оси).
- Выброс руды и самоцветов — частицы в Godot, в модели их нет. Они вылетают из земли у нижнего конца шнека и по дуге падают на клетку, которая подросла; самоцветы — разноцветные, как на земле (`game/scripts/Terrain/OreSources.cs`). Свечение — emissive-материал, как у других источников (см. [требования](../generation_requirements.md), раздел 4).
- Самоцветный источник — тот же `ore_source` с другим материалом бура (см. [README](README.md), вопрос 3).
- Кристаллы непрозрачные или полупрозрачные с низким коэффициентом. Прозрачность сильно дорожает рендер.
- Руда должна быть самым ярким тёплым пятном на зимней карте. Проверяем на камере RA2.
- Цвет команды не нужен: ресурсы нейтральные.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `ore_cluster.glb` | Blender, скрипт `tools/blender/families/ore_clusters.py` | 2026-10-30 | готово |
| `gem_cluster.glb` | то же | 2026-10-30 | готово |
| `ore_source.glb` | Blender, скрипт `tools/blender/families/ore_source.py` | 2026-10-30 | готово: высота 1,8 (ниже на 40 %) |

## Открытые вопросы

1. ~~Один цвет на тип самоцветов или разные оттенки для разной ценности.~~ Решено 2026-10-30: разноцветные вперемешку, см. [appearance](appearance.md).
2. Одна модель для обоих источников или отдельный `gem_source` (см. [README](README.md), вопрос 3).
