# 09. Мелкие объекты и декор: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Мелкие объекты и декор](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей декора: валун, фонарь, столб, знак, бочка, куст, машина. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|
| `prop_boulder` | 1x1 | 0,5 | ≤ 1 000 | да |
| `prop_lamp_post` | 1x1 | 2 | ≤ 1 500 | да (столб) |
| `prop_telegraph_pole` | 1x1 | 2 | ≤ 1 000 | да (столб) |
| `prop_road_sign` | 1x1 | 1,5 | ≤ 800 | нет |
| `prop_barrel_explosive` | 1x1 | 0,5 | ≤ 800 | да |
| `prop_snow_bush` | 1x1 | 0,5 | ≤ 800 | нет |
| `prop_car_wreck` | 1x1 | 0,5 | ≤ 4 000 | да |

Дорожный знак тот же, что у [городских элементов](../../city_elements/16_road_sign/README.md). Генерируем один раз. Лампа светит через emissive-материал в Godot, в модели стекло погашено.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/props/`.

```text
Concept art reference for street props in a winter city corner: a snow-covered boulder, a rusty street lamp post, a wooden telegraph pole, a blue road sign, a red explosive barrel with a hazard stripe, a snow-covered bush and an abandoned rusted car. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text, no readable writing on any sign.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `prop_boulder`

```text
A large weathered grey-brown boulder with a thick cap of snow on top, about one unit wide and half a unit tall, rough realistic rock surface. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `prop_lamp_post`

```text
A tall rusty metal street lamp post about two units tall, with a plain glass lantern housing on top, frost on the metal, a short base flange. The glass is unlit. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.3. `prop_telegraph_pole`

```text
A weathered wooden telegraph pole about two units tall with one horizontal crossarm and ceramic insulators, no wires. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Провода — линии в коде, не в модели.

### 3.4. `prop_road_sign`

```text
A road sign on a thin metal post about one and a half units tall: a flat blue rectangular sign with a plain white arrow symbol, no text, no letters, frost on the post. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.5. `prop_barrel_explosive`

```text
A red steel oil barrel about half a unit tall, with a black and yellow hazard stripe band and a simple warning symbol, dented sides, rust at the bottom. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.6. `prop_snow_bush`

```text
A bare winter shrub with thin twigs covered in patches of snow, about half a unit tall and one unit wide. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.7. `prop_car_wreck`

```text
An abandoned rusted sedan car about one unit long and half a unit tall, flat tyres, missing windows, snow drifted on the roof and hood. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 1x1`. Бюджет — как у техники (M1, 4.7).

## 4. Наши требования

- Блокирующие объекты (камень, столб, бочка, машина) выглядят массивно. Мелкие (знак, куст) — тонко. Это правило читаемости из [appearance.md](appearance.md).
- Пивот — центр клетки, низ на земле.
- Цвета команды нет.
- Взрывоопасная бочка — красная с полосой, чтобы выделялась. Реакция на взрыв — в коде и эффектах.
- Знаки — только символы, без текста (вопрос из [16_road_sign](../../city_elements/16_road_sign/appearance.md)).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `prop_boulder.glb` | | | |
| `prop_lamp_post.glb` | | | |
| `prop_telegraph_pole.glb` | | | |
| `prop_road_sign.glb` | | | |
| `prop_barrel_explosive.glb` | | | |
| `prop_snow_bush.glb` | | | |
| `prop_car_wreck.glb` | | | |

## Открытые вопросы

1. Взрывоопасные бочки на зимней карте: красный цвет выделяется, но не слишком ли заметно.
