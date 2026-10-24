# 02. Типы земли: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Типы земли](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и текстур земли. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `snow_clean` | текстура, бесшовная | 1024² | основа (чистый снег), в маске — остаток |
| `frozen_grass` | текстура, бесшовная | 1024² | канал R маски (трава) |
| `mud` | текстура, бесшовная | 1024² | канал G маски (грязь, бездорожье) |
| `beach_gravel` | текстура, бесшовная | 1024² | тип «берег» |
| `rock_field` | текстура, бесшовная | 1024² | тип «скала» |

3D-моделей нет. Каналы маски описаны в M7 и M1 (4.4): R — трава, G — грязь, B — дорога, остальное — снег. Дорога — в [04_roads](../04_roads/prompts.md).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/land_types/`.

```text
Concept art reference for the terrain materials of a winter map: a top-down material sheet of five zones side by side: fresh clean snow, snow with patches of frozen grass, frozen dark mud, grey-brown beach gravel at a water's edge, and a dark broken rock field. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no text, no people.
```

## 3. Текстуры (генератор изображений, не 3D)

Все промты заканчиваются одинаково: вид сверху, без теней, 1024×1024, без текста.

### 3.1. `snow_clean`

```text
Seamless tileable texture of clean fresh snow on flat ground, fine grain, faint blue shade in small depressions, almost no footprints. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `frozen_grass`

```text
Seamless tileable texture of frozen brown-grey grass with frost on the blades and sparse snow between the tufts. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.3. `mud`

```text
Seamless tileable texture of dark brown mud with cracks and small frozen puddles. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.4. `beach_gravel`

```text
Seamless tileable texture of grey-brown gravel and pebbles with a thin ice crust on the sandy patches. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.5. `rock_field`

```text
Seamless tileable texture of dark grey broken rock slabs and boulder fragments, no vegetation. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Все текстуры бесшовные, 1024², PNG. Без запечённых теней и бликов.
- Тип земли — игровое свойство, текстура — только картинка. `beach_gravel` и `rock_field` — отдельные типы, в маску не входят.
- Масштаб тайлинга у каждого слоя свой (M1, 4.4), подбираем в Godot.
- Цвет команды не нужен: это земля.
- Лёд (озёра и реки) — в [03_water](../03_water/prompts.md), не здесь.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `snow_clean.png` | заглушка (скрипт) | 2026-10-23 | не финальная, заменить генератором |
| `frozen_grass.png` | заглушка (скрипт) | 2026-10-23 | не финальная, заменить генератором |
| `mud.png` | заглушка (скрипт) | 2026-10-23 | не финальная, заменить генератором |
| `beach_gravel.png` | заглушка (скрипт) | 2026-10-23 | не финальная, заменить генератором |
| `rock_field.png` | заглушка (скрипт) | 2026-10-23 | не финальная, заменить генератором |
| `snow_powder.png` | Poly Haven `snow_02`, CC0 | 2026-10-24 | в игре вместо `snow_clean` |
| `sparse_grass.png` | Poly Haven `sparse_grass`, CC0 | 2026-10-24 | в игре вместо `frozen_grass`, иней — в шейдере |
| `dirt_snow.png` | Poly Haven `snow_03`, CC0 | 2026-10-24 | в игре: грязь (`dirt`) вместо `mud` |
| `coast_sand.png` | Poly Haven `coast_sand_03`, CC0 | 2026-10-24 | в игре вместо `beach_gravel` |
| `rock_ground.png` | Poly Haven `rock_ground`, CC0 | 2026-10-24 | в игре вместо `rock_field` |
| `asphalt_snow.png` | Poly Haven `asphalt_snow`, CC0 | 2026-10-24 | в игре: асфальт (`asphalt`), раньше не было |

## Открытые вопросы

1. ~~Нужна ли текстура грязи для зимнего климата, или хватит канала G в маске.~~ Нужна: материал `dirt` (`dirt_snow.png`), маски нет — смешение по слою `texture` ([M7](../../implementation/M7_maps.md), 4.12).
