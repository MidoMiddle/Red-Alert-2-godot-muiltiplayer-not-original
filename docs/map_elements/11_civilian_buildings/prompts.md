# 11. Гражданские здания и гарнизоны: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Гражданские здания и гарнизоны](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей базовых гражданских зданий: малый дом, многоэтажка, памятник, руины. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|
| `building_house_small` | 2x2 | 2,5 | ≤ 8 000 | да, 3 | да (козырёк над крыльцом) |
| `building_house_small_damaged` | 2x2 | 2,5 | ≤ 8 000 | — | да |
| `building_house_ruins` | 2x2 | 0,8 | ≤ 4 000 | нет | нет |
| `building_apartment_block` | 3x3 | 6 | ≤ 15 000 | да, 8 | да (парапет по верху крыши) |
| `building_monument` | 2x2 | 3 | ≤ 5 000 | нет | нет |

Остальные городские здания (школа, офис, церковь, магазин, киоск и др.) — в [городских элементах](../../city_elements/README.md). Их промты будут отдельно.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/buildings_civil/`.

```text
Concept art reference for a cold-war style city street in winter: a two-storey brick house with a pitched roof covered in snow, a five-storey apartment block with rows of identical windows, and a stone monument on a small square. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text, no signs with writing.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `building_house_small`

```text
A small two-storey timber house, the upper storey under a steep gable roof of dark green sheet covered in thick snow, its gable to the street: ochre weatherboards on a stone plinth, white carved window frames with curtains, dark green shutters, a brick chimney, a wooden porch with a small gable roof over the door, a woodpile under a lean-to, a low dark picket fence with snow on its rail. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

### 3.2. `building_house_small_damaged`

Тот же дом (промт 3.1) с добавлением:

```text
Damaged: broken window glass, cracks in the brick, soot marks above the windows, part of the roof collapsed on one side.
```

Нормализация: `--kind building --footprint 2x2`.

### 3.3. `building_house_ruins`

```text
Ruins of a small brick house: low broken walls, rubble piles, charred roof beams, snow patches between the bricks, about one unit tall at most. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

### 3.4. `building_apartment_block`

```text
A five-storey red brick apartment block on a grey concrete plinth with a flat roof under snow inside a plain parapet band, rows of identical windows in white frames, two entrances with concrete canopies and the small windows of the stairs above them, concrete balconies with pale fronts and snow on them, brick vent shafts and aerials on the roof. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`.

### 3.5. `building_monument`

```text
A stone monument on a stepped pedestal in a small square: a tall plain stele with a rounded top, snow on the steps, no inscriptions and no symbols. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта, низ подложки на земле (M1, 4.7). Здание стоит на бетонной подложке (узел `base`); хижина без бетона — на участке земли или песка другого оттенка ([model_standards](../../art/model_standards.md), раздел 3, «Фундамент»).
- **Узлы:** `body`, `roof`, `team_mask`, `base`; у покинутого здания — точки огня `fire_1…`. Маска — козырёк над крыльцом у малого дома и парапет по верху крыши у многоэтажки: тонкую полосу на стене после сжатия по высоте не видно. Деталь уже есть в модели, маска задаёт её цвет.
- **Гарнизон** — это поведение, не модель. Силуэты бойцов в окнах и вспышки делаем кодом и эффектами. Окна в модели должны быть видны, иначе здание не читается как здание с окнами (см. [appearance.md](appearance.md)).
- **Шесть состояний** — отдельные модели: целое, занятое пехотой (`_occupied`), повреждённое (`_damaged`: от четверти до половины здоровья), повреждённое и занятое (`_damaged_occupied`), покинутое (`_abandoned`: меньше четверти здоровья, пехота вышла) и руины (`_ruins`) на месте разрушенного здания. Игра выбирает модель по здоровью и гарнизону: поля `damaged`, `damaged_occupied`, `occupied`, `abandoned`, `ruins` в [models.yaml](../../../game/data/visuals/models.yaml).
- **Бюджеты** ниже лимита M1 для зданий (≤ 20 000). Малые здания не должны тратить лимит.
- **Текстуры:** 2048² для многоэтажки, 1024² для малого дома и руин (M1, 4.7).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `building_house_small.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_house.py`) | 2026-10-30 | сделано заново, 2 656 треугольников |
| `building_house_small_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_house.py`) | 2026-10-30 | сделано заново, 2 208 треугольников |
| `building_house_small_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_house.py`) | 2026-10-30 | сделано заново, 1 698 треугольников |
| `building_house_small_damaged_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_house.py`) | 2026-10-30 | сделано заново, 2 864 треугольников |
| `building_house_small_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_house.py`) | 2026-10-30 | сделано заново, 3 312 треугольников |
| `building_house_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_house.py`) | 2026-10-30 | сделано заново, 1 176 треугольников |
| `building_apartment_block.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_block.py`) | 2026-10-30 | сделано заново, 8 428 треугольников |
| `building_apartment_block_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_block.py`) | 2026-10-30 | сделано заново, 7 468 треугольников |
| `building_apartment_block_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_block.py`) | 2026-10-30 | сделано заново, 6 516 треугольников |
| `building_apartment_block_damaged_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_block.py`) | 2026-10-30 | сделано заново, 7 984 треугольников |
| `building_apartment_block_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_block.py`) | 2026-10-30 | сделано заново, 8 944 треугольников |
| `building_apartment_block_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/civil_block.py`) | 2026-10-30 | сделано заново, 1 340 треугольников |
| `building_monument.glb` | | | |

## Открытые вопросы

1. Нужны ли два-три варианта фасада малого дома, чтобы улица не выглядела копией.
2. Оставляем ли руины блокирующими проход (общий вопрос из [11_civilian_buildings](README.md)).
