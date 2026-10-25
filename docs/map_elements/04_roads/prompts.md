# 04. Дороги: промты для генерации

> Статус: черновик · Создан: 2026-10-22 · Обновлён: 2026-10-25
> Родительский документ: [Дороги](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для текстур дорог и троп. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Вид | Климат | Размер | Статус |
|---|---|---|---|---|
| `road_asphalt` | асфальтированная дорога | город | 1024² | есть: `asphalt.png` |
| `road_dirt` | грунтовка | умеренный | 1024² | нужна |
| `road_packed_snow` | зимник | зима | 1024² | нужна |
| `road_gravel` | гравийная дорога | пустыня, побережье | 1024² | нужна |
| `footpath_snow` | тропинка | зима | 1024² | нужна |
| `footpath_grass` | тропинка | умеренный | 1024² | нужна |
| `footpath_sand` | тропинка | пустыня, побережье | 1024² | нужна |
| `trail_snow` | протоптанная тропа | зима | 1024² | нужна |
| `trail_grass` | протоптанная тропа | умеренный | 1024² | нужна |
| `trail_sand` | протоптанная тропа | пустыня, побережье | 1024² | нужна |

3D-моделей нет. Дорога и тропа — тип земли и текстура из легенды карты. Разметка — декалями, позже. Пешеходный переход — у [городских элементов](../../city_elements/18_pedestrian_crossing/README.md).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/roads/`.

```text
Concept art reference for a winter road between snowbanks: a packed snow road with two dark tyre tracks running along it, low snowbanks on both sides, a wooden fence post at the edge. Three-quarter view from 45 degrees, looking down at about 30 degrees. Soft cold light, realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `road_asphalt`

Файл `asphalt.png` уже есть. Промт — на случай замены.

```text
Seamless tileable texture of worn grey asphalt with fine aggregate and small cracks, no painted lines. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `road_dirt`

```text
Seamless tileable texture of an unpaved country dirt road: light brown compacted earth with two darker wheel ruts running lengthwise and a few small scattered stones, no grass. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.3. `road_packed_snow`

```text
Seamless tileable texture of a packed snow road surface with two darker wheel track grooves running lengthwise, compacted and slightly icy. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.4. `road_gravel`

```text
Seamless tileable texture of a gravel road in a dry desert: pale grey-beige loose gravel packed flat, with two darker tyre tracks running lengthwise. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.5. `footpath_snow`

```text
Seamless tileable texture of a narrow footpath trodden through deep snow: a thin strip of compacted grey-white snow with footprints and a few dark stones at the edges. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.6. `footpath_grass`

```text
Seamless tileable texture of a narrow footpath worn into a meadow: a bare pale earth strip with trampled short grass at the edges. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.7. `footpath_sand`

```text
Seamless tileable texture of a narrow footpath across desert sand: a thin strip of packed darker sand with footprints. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.8. `trail_snow`

```text
Seamless tileable texture of a wide trodden trail through snow: a broad band of compacted snow and ice with many overlapping footprints and sled marks. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.9. `trail_grass`

```text
Seamless tileable texture of a wide trodden trail across a field: bare compacted earth with flattened grass and no tyre tracks. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.10. `trail_sand`

```text
Seamless tileable texture of a wide caravan trail across desert: a broad band of packed pale sand with worn ruts from many feet and hooves. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Ширина дороги и тропы — одна клетка. Текстура тайлится без видимого шва на краю клетки.
- Колеи дороги идут вдоль оси текстуры. Поворот по направлению дороги — открытый вопрос.
- Вид каждой дороги и тропы отличается от бездорожья того же климата: `road_dirt` не равен `dirt_brown`, `road_gravel` не равен `sand_gravel`.
- Бонус скорости задаёт данные (`terrain.yaml`), текстура его не показывает.
- Цвет команды не нужен.
- Тона — по [01_colour](../../art/style_guide/01_colour.md) (цвета дорог по климату).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `road_asphalt.png` | — | — | есть (`asphalt.png`) |
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
