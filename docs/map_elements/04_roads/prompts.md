# 04. Дороги: промты для генерации

> Статус: черновик · Создан: 2026-10-22 · Обновлён: 2026-10-30
> Родительский документ: [Дороги](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для текстур дорог и троп. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Вид | Климат | Размер | Статус |
|---|---|---|---|---|
| `road_highway` | шоссе | город, умеренный, пустыня, побережье | 1024² | есть |
| `road_highway_snow` | шоссе | зима | 1024² | есть |
| `road_asphalt` | асфальтированная дорога | город | 1024² | есть: `asphalt.png` |
| `road_street` | улочка, старый асфальт | город | 1024² | есть |
| `road_street_cobble` | улочка, брусчатка | город | 1024² | есть |
| `highway_guardrail` | отбойник шоссе (3D-модель: целый, помятый, сломанный) | все | ≤ 600 треугольников, 512² | есть |
| `road_dirt` | грунтовка | умеренный | 1024² | нужна |
| `road_packed_snow` | зимник | зима | 1024² | нужна |
| `road_gravel` | гравийная дорога | пустыня, побережье | 1024² | нужна |
| `footpath_snow` | тропинка | зима | 1024² | нужна |
| `footpath_grass` | тропинка | умеренный | 1024² | нужна |
| `footpath_sand` | тропинка | пустыня, побережье | 1024² | нужна |
| `trail_snow` | протоптанная тропа | зима | 1024² | нужна |
| `trail_grass` | протоптанная тропа | умеренный | 1024² | нужна |
| `trail_sand` | протоптанная тропа | пустыня, побережье | 1024² | нужна |

Дорога и тропа — тип земли и текстура из легенды карты. Единственная 3D-модель — отбойник шоссе (объект карты). Разметка — декалями, позже. Пешеходный переход — у [городских элементов](../../city_elements/18_pedestrian_crossing/README.md).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/roads/`.

```text
Concept art reference for a winter road between snowbanks: a packed snow road with two dark tyre tracks running along it, low snowbanks on both sides, a wooden fence post at the edge. Three-quarter view from 45 degrees, looking down at about 30 degrees. Soft cold light, realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `road_highway`

Одна клетка — одна полоса движения; шоссе — три клетки этой текстуры рядом. Разметки нет, она — декалями.

```text
Seamless tileable texture of smooth dark highway asphalt, newer and darker than a town road, fine even aggregate, faint darker wheel-worn bands running lengthwise, no painted lines, no cracks. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `road_highway_snow`

```text
Seamless tileable texture of a dark highway asphalt lane under a thin dusting of snow, two darker bands where the tyres have cleared it running lengthwise, a little slush, no painted lines. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.3. `road_asphalt`

Файл `asphalt.png` уже есть. Промт — на случай замены.

```text
Seamless tileable texture of worn grey asphalt with fine aggregate and small cracks, no painted lines. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.4. `road_street`

Улочка — одна клетка; тротуары по сторонам — существующий материал плит (`slabs`).

```text
Seamless tileable texture of an old narrow town street: lighter grey asphalt with many square patches of newer darker asphalt, a few small cracks and worn spots, no painted lines. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.5. `road_street_cobble`

```text
Seamless tileable texture of an old town street paved with small grey granite cobblestones in even rows, worn smooth in the middle, a little dirt in the joints, no painted lines. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.6. `highway_guardrail` (3D-модель)

Объект карты на одну клетку, вдоль края шоссе. Пивот — центр клетки на земле, барьер вдоль оси X у края клетки. Узел `body`; три файла: целый, помятый, сломанный. Бюджет ≤ 600 треугольников, текстура 512².

```text
A short section of a highway guardrail, one unit long: a galvanised corrugated steel W-beam on three short steel posts with small reflectors, light grey metal with a few rust spots. Realistic physically based materials, clean readable silhouette, game-ready asset, single isolated object on a plain light grey background.
```

### 3.7. `road_dirt`

```text
Seamless tileable texture of an unpaved country dirt road: light brown compacted earth with two darker wheel ruts running lengthwise and a few small scattered stones, no grass. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.8. `road_packed_snow`

```text
Seamless tileable texture of a packed snow road surface with two darker wheel track grooves running lengthwise, compacted and slightly icy. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.9. `road_gravel`

```text
Seamless tileable texture of a gravel road in a dry desert: pale grey-beige loose gravel packed flat, with two darker tyre tracks running lengthwise. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.10. `footpath_snow`

```text
Seamless tileable texture of a narrow footpath trodden through deep snow: a thin strip of compacted grey-white snow with footprints and a few dark stones at the edges. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.11. `footpath_grass`

```text
Seamless tileable texture of a narrow footpath worn into a meadow: a bare pale earth strip with trampled short grass at the edges. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.12. `footpath_sand`

```text
Seamless tileable texture of a narrow footpath across desert sand: a thin strip of packed darker sand with footprints. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.13. `trail_snow`

```text
Seamless tileable texture of a wide trodden trail through snow: a broad band of compacted snow and ice with many overlapping footprints and sled marks. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.14. `trail_grass`

```text
Seamless tileable texture of a wide trodden trail across a field: bare compacted earth with flattened grass and no tyre tracks. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.15. `trail_sand`

```text
Seamless tileable texture of a wide caravan trail across desert: a broad band of packed pale sand with worn ruts from many feet and hooves. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Ширина дороги и тропы — одна клетка, шоссе — три клетки одной текстуры. Текстура тайлится без видимого шва на краю клетки.
- Три асфальта различимы: шоссе темнее и ровнее, асфальтированная дорога — средний тон, улочка светлее и в заплатках.
- Колеи дороги идут вдоль оси текстуры. Поворот по направлению дороги — открытый вопрос.
- Вид каждой дороги и тропы отличается от бездорожья того же климата: `road_dirt` не равен `dirt_brown`, `road_gravel` не равен `sand_gravel`.
- Бонус скорости задаёт данные (`terrain.yaml`), текстура его не показывает.
- Цвет команды не нужен.
- Тона — по [01_colour](../../art/style_guide/01_colour.md) (цвета дорог по климату).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `road_highway.png` | Blender + numpy, скрипт `tools/blender/families/road_textures.py` | 2026-10-30 | готово |
| `road_highway_snow.png` | Blender + numpy, скрипт `tools/blender/families/road_textures.py` | 2026-10-30 | готово |
| `road_asphalt.png` | — | — | есть (`asphalt.png`) |
| `road_street.png` | Blender + numpy, скрипт `tools/blender/families/road_textures.py` | 2026-10-30 | готово |
| `road_street_cobble.png` | Blender + numpy, скрипт `tools/blender/families/road_textures.py` | 2026-10-30 | готово |
| `highway_guardrail.glb` (и `_damaged`, `_ruins`) | Blender, скрипт `tools/blender/families/highway_guardrail.py` | 2026-10-30 | готово |
| `road_dirt.png` | | | нет |
| `road_packed_snow.png` | | | нет |
| `road_gravel.png` | | | нет |
| `footpath_snow.png` | | | нет |
| `footpath_grass.png` | | | нет |
| `footpath_sand.png` | | | нет |
| `trail_snow.png` | | | нет |
| `trail_grass.png` | | | нет |
| `trail_sand.png` | | | нет |

---

## Открытые вопросы

1. Поворот колей по направлению дороги: отдельные текстуры на каждый поворот или поворот UV в шейдере.
2. Караванная тропа: `trail_sand` достаточно или нужна отдельная текстура для колей верблюдов.
3. ~~Улочка: брусчатка или старый асфальт.~~ Решено 2026-10-30: оба, `road_street` и `road_street_cobble`.
