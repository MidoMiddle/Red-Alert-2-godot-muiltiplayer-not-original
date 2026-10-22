# 13. Скот: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Скот](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей коровы и овцы. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|
| `civ_cow` | 1x1 | 0,8 | ≤ 1 500 | нет |
| `civ_sheep` | 1x1 | 0,5 | ≤ 1 000 | нет |

Анимация — клипы покоя, ходьбы, бега и падения. Скелет четвероногого — авториг генератора или упрощённый вручную. Mixamo для животных не подходит (M1, 4.13 — для людей).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for grazing livestock on a green meadow in warm daylight: a black and white dairy cow and a white woolly sheep with a dark face, standing side by side. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `civ_cow`

```text
A dairy cow about one unit long and eight tenths of a unit tall, black and white patches, standing relaxed on four legs with its head slightly lowered. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `civ_sheep`

```text
A white woolly sheep about one unit long and half a unit tall, dark grey face, standing relaxed on four legs. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Не блокирует проход.
- Пятна коровы — текстура; варианты окраса — варианты текстуры.
- Цвета команды нет.
- Бюджет: корова ≤ 1 500, овца ≤ 1 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_cow.glb` | | | |
| `civ_sheep.glb` | | | |

## Открытые вопросы

Открытых вопросов по моделям нет.
