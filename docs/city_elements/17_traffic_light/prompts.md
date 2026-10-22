# 17. Светофор: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Светофор](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели светофора. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_traffic_light` | объект | 1x1 | 2 | ≤ 1 500 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a traffic light on a tall grey pole at a city crossroads in neutral daylight: a black housing with three round lamps (red on top, yellow in the middle, green at the bottom), each lamp with a plain hood, all lamps unlit. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no symbols.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_traffic_light`

```text
A traffic light on a tall grey pole about two units tall: a black housing with three round lamps, red on top, yellow in the middle and green at the bottom, each lamp with a plain hood above it, all lamps unlit, no text, no symbols. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `post` (столб), `housing` (коробка), `lamp_red`, `lamp_yellow`, `lamp_green`.
- **Лампы — отдельные узлы.** Цикл сигналов переключает их в коде, поэтому каждая лампа должна переключаться отдельно.
- **Лампы погашены в модели.** Свечение — emissive-материал в Godot, как у фонарей ([09_props](../../map_elements/09_props/prompts.md)).
- **Цикл сигналов — клиентский.** В симуляцию не входит, пока не решено иначе (см. [концепцию](README.md), вопрос 2). Модель от этого не меняется.
- **Бюджет** ≤ 1 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_traffic_light.glb` | | | |

## Открытые вопросы

1. Нужна ли подсветка асфальта цветом сигнала ночью.
2. Влияют ли светофоры на движение мирного транспорта. Если да, цикл переходит в симуляцию, и узлы ламп остаются теми же.
