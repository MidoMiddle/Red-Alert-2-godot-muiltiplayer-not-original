# 13. Мирные жители и гражданская техника: промты для генерации

> Статус: черновик · Создан: 2026-10-22 · Дополнен: 2026-11-01 (жители каждого места, грузовик)
> Родительский документ: [Мирные жители и гражданская техника](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей мирных жителей, машин и собаки. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Где | Футпринт | Высота (ориентир) | Бюджет LOD0 | Цвет команды |
|---|---|---|---|---|---|---|
| `civ_man` | человек | город; база для пляжных | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_woman` | человек | город; база для пляжных | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_winter_man` | человек | зимний посёлок | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_winter_woman` | человек | зимний посёлок | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_farmer` | человек | деревня | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_desert_man` | человек | пустыня | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_desert_woman` | человек | пустыня | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_lifeguard` | человек | пляж | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_car_sedan` | машина | город, посёлок, деревня | 1x1 | 0,5 | ≤ 4 000 | нет |
| `civ_bus` | машина | город, посёлок | 2x1 | 0,8 | ≤ 8 000 | нет |
| `civ_truck` | машина | деревня, пустыня, город | 2x1 | 0,8 | ≤ 6 000 | нет |
| `civ_dog` | животное (по желанию) | город, посёлок | 1x1 | 0,4 | ≤ 1 500 | нет |

Отдыхающие на пляже — перекраска `civ_man` и `civ_woman`, их промты в [Отдыхающих](../../beach_elements/10_beachgoers/prompts.md). Скот и верблюды — в своих наборах ([скот](../../temperate_elements/13_livestock/prompts.md), [верблюды](../../desert_elements/11_camels_vultures/prompts.md)).

Цвет одежды и машин — варианты текстуры, не новые модели. Такси, полицейская, скорая и пожарная машина — окраски `civ_car_sedan` (пожарная — `civ_truck`), без надписей и логотипов.

## 2. Картинки-референсы (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/civilians/`.

**Город:**

```text
Concept art reference for a city street with civilians: a man in a light jacket and jeans and a woman in a coat with a shopping bag walking along the sidewalk, people waiting at a bus stop, a small sedan parked at the curb, a bus, and a dog on a leash. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no weapons, no uniforms, no text.
```

**Зимний посёлок:**

```text
Concept art reference for a winter village street with civilians: a man and a woman in warm puffer jackets, knitted hats and scarves walking, a small sedan with snow on the roof parked at the curb, and a bus at a bus stop. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no weapons, no uniforms, no text.
```

**Деревня и пустыня:**

```text
Concept art reference with two panels: on the left a farmer in overalls, a checked shirt, rubber boots and a cap carrying a bucket by a farmhouse and a pickup truck; on the right a man and a woman in long light robes and head scarves by an adobe house and a well. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no weapons, no uniforms, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9. Людей делаем в A-позе, чтобы риггать ([§ 4](#4-наши-требования)).

### 3.1. `civ_man`

```text
A civilian man in a light casual jacket, a plain shirt, jeans and sneakers, standing in an A-pose with arms slightly away from the body, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

Нормализация: пехота — отдельного типа в скрипте пока нет (M1, 4.8). Используем `--kind prop --footprint 1x1` и уточняем в M1. Так же для всех людей ниже.

### 3.2. `civ_woman`

```text
A civilian woman in a knee-length coat over a simple dress, flat shoes and a shoulder bag, standing in an A-pose, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

### 3.3. `civ_winter_man`

```text
A civilian man in a warm winter puffer jacket, knitted hat and scarf, jeans and winter boots, standing in an A-pose with arms slightly away from the body, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

### 3.4. `civ_winter_woman`

```text
A civilian woman in a long dark winter coat, a knitted hat, a scarf and a shoulder bag, standing in an A-pose, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

### 3.5. `civ_farmer`

```text
A farmer in denim overalls over a checked shirt, rubber boots and a cap, standing in an A-pose, human proportions, empty hands. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

Ведро и вилы — отдельные мелкие модели в руку (узел `hand_r`), не часть фигуры.

### 3.6. `civ_desert_man`

```text
A civilian man in a long light loose robe, a head scarf and sandals, standing in an A-pose, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

### 3.7. `civ_desert_woman`

```text
A civilian woman in a long loose dress in earth tones, a light head scarf and sandals, standing in an A-pose, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

### 3.8. `civ_lifeguard`

```text
A beach lifeguard in red swim shorts and a white t-shirt with a plain red cross, a whistle on a cord, barefoot, standing in an A-pose, human proportions. No text, no logos, no weapons. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

### 3.9. `civ_car_sedan`

```text
A small sedan car in muted colours, headlights off, four wheels, realistic and game-ready. Realistic physically based materials, clean readable silhouette, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 1x1`. Зимний вариант — иней на крыше в текстуре.

### 3.10. `civ_bus`

```text
A city bus in muted colours with a row of windows, four wheels, realistic and game-ready. Realistic physically based materials, clean readable silhouette, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 2x1`.

### 3.11. `civ_truck`

```text
A small civilian delivery truck with a cab and a closed cargo box, muted colours, four wheels, realistic and game-ready. No text, no logos. Realistic physically based materials, clean readable silhouette, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 2x1`.

### 3.12. `civ_dog` (по желанию)

```text
A small dog with a thick coat, standing pose, realistic and game-ready. Realistic physically based materials, clean readable silhouette, single isolated figure on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Людей риггим** по пайплайну M1 (4.13): A- или T-поза, скелет через Mixamo, клипы `idle`, `walk`, `run`, `die_0..2` из библиотеки Mixamo; для поз покоя ещё `sit`, `crouch` (укрытие), `wave`, у пляжных `prone`. Генерацией анимаций не занимаемся.
- **Пивот:** человек — центр на земле; машина — центр основания на земле.
- **Цвет команды** не нужен, мирные жители нейтральные.
- **Одежда:** варианты цвета — текстуры, одна модель на тип.
- **Без крови и жестокости.** Падение — поза, без визуальных ран (тон игры, см. [appearance.md](appearance.md)).
- **Без надписей и логотипов**, в том числе на особых машинах.
- **Полигоны:** люди укладываются в пехотный бюджет (1 500), машины — в технику (8 000 для автобуса, 6 000 для грузовика, 4 000 для легковой).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_man.glb` | | | |
| `civ_woman.glb` | | | |
| `civ_winter_man.glb` | | | |
| `civ_winter_woman.glb` | | | |
| `civ_farmer.glb` | | | |
| `civ_desert_man.glb` | | | |
| `civ_desert_woman.glb` | | | |
| `civ_lifeguard.glb` | | | |
| `civ_car_sedan.glb` | | | |
| `civ_bus.glb` | | | |
| `civ_truck.glb` | | | |
| `civ_dog.glb` (по желанию) | | | |

## Открытые вопросы

1. Нужны ли мирные жители в мультиплеере или только визуальная массовка в клиенте ([README](README.md), § 6).
2. Ездят ли по улицам автобусы как гражданская техника, или только стоят на остановках.
