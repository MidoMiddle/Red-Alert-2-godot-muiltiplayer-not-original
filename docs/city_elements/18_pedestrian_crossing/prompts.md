# 18. Пешеходный переход: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пешеходный переход](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры разметки перехода. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

Модели нет: переход — разметка на дороге, то есть декаль поверх дороги. Нужна одна текстура:

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `crosswalk_stripes` | текстура разметки (декаль), 2D | 1024² | белые полосы и стоп-линия поверх дороги |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a pedestrian crossing on a city road in neutral daylight: wide white zebra stripes across dark grey asphalt, a solid white stop line before the crossing, a traffic light visible at the edge. Top-down view close to 45 degrees, realistic materials, square image, no people, no text.
```

## 3. Текстура (генератор изображений, не 3D)

### 3.1. `crosswalk_stripes`

```text
Seamless texture of white zebra crossing stripes painted across dark grey asphalt, wide bold stripes evenly spaced and parallel to each other, with a solid white stop line along one edge. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

Прозрачный фон: декаль накладывается поверх асфальта. Фон должен быть прозрачным там, где нет полос, или асфальтовым, если накладываем без альфа-канала (решаем в Godot).

## 4. Наши требования

- **Тип земли не меняется.** Под переходом остаётся дорога ([04_roads](../../map_elements/04_roads/prompts.md)), проходимость и скорость те же.
- **Декаль, не объект.** В `climates/city/map_objects.yaml` записи нет.
- **Полосы поперёк дороги**, стоп-линия ближе к светофору.
- **Цвет команды** не нужен.
- **Текстура** 1024² с прозрачным или асфальтовым фоном, без запечённых теней.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `crosswalk_stripes.png` | | | |

## Открытые вопросы

1. Нужна ли островная разметка посередине широких дорог.
2. Ходят ли мирные жители только по переходам (поведение, решаем в M11, [13_civilians](../../map_elements/13_civilians/prompts.md)).
