# 13. Мирные жители и гражданская техника: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Мирные жители и гражданская техника](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей мирных жителей, машин и собаки. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Цвет команды |
|---|---|---|---|---|---|
| `civ_man` | человек | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_woman` | человек | 1x1 | 0,5 | ≤ 1 500 | нет |
| `civ_car_sedan` | машина | 1x1 | 0,5 | ≤ 4 000 | нет |
| `civ_bus` | машина | 2x1 | 0,8 | ≤ 8 000 | нет |
| `civ_dog` | животное (по желанию) | 1x1 | 0,4 | ≤ 1 500 | нет |

Цвет одежды и машин — варианты текстуры, не новые модели. Грузовик не делаем, пока нет сценария, где он нужен.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/civilians/`.

```text
Concept art reference for a winter street with civilians: a man and a woman in warm jackets and hats walking, a small sedan parked at the curb, a bus at a bus stop, and a dog on a leash. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no weapons, no uniforms, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `civ_man`

```text
A civilian man in a warm winter jacket, knitted hat and scarf, jeans and boots, standing in an A-pose with arms slightly away from the body, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

Нормализация: пехота — отдельного типа в скрипте пока нет (M1, 4.8). Используем `--kind prop --footprint 1x1` и уточняем в M1.

### 3.2. `civ_woman`

```text
A civilian woman in a long dark winter coat and a knitted hat, a shoulder bag, standing in an A-pose, human proportions. No weapons, no uniform. Realistic physically based materials, clean readable silhouette, game-ready asset, single figure on a plain light grey background.
```

Нормализация: как у `civ_man`.

### 3.3. `civ_car_sedan`

```text
A small sedan car in muted colours, snow on the roof, headlights off, four wheels, realistic and game-ready. Realistic physically based materials, clean readable silhouette, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 1x1`.

### 3.4. `civ_bus`

```text
A city bus in muted colours with a row of windows, snow on the roof, four wheels, realistic and game-ready. Realistic physically based materials, clean readable silhouette, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 2x1`.

### 3.5. `civ_dog` (по желанию)

```text
A small dog with a thick winter coat, standing pose, realistic and game-ready. Realistic physically based materials, clean readable silhouette, single isolated figure on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Людей риггим** по пайплайну M1 (4.13): A- или T-поза, скелет через Mixamo, клипы `idle`, `walk`, `run`, `die_0..2` из библиотеки Mixamo. Генерацией анимаций не занимаемся.
- **Пивот:** человек — центр на земле; машина — центр основания на земле.
- **Цвет команды** не нужен, мирные жители нейтральные.
- **Одежда:** варианты цвета — текстуры, одна модель на тип.
- **Без крови и жестокости.** Падение — поза, без визуальных ран (тон игры, см. [appearance.md](appearance.md)).
- **Полигоны:** люди укладываются в пехотный бюджет (1 500), машины — в технику (8 000 для автобуса, 4 000 для легковой).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_man.glb` | | | |
| `civ_woman.glb` | | | |
| `civ_car_sedan.glb` | | | |
| `civ_bus.glb` | | | |
| `civ_dog.glb` (по желанию) | | | |

## Открытые вопросы

1. Нужны ли мирные жители в мультиплеере или только визуальная массовка в клиенте.
2. Ездят ли по улицам автобусы как гражданская техника, или только стоят на остановках.
