# 08. Каменные руины: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Каменные руины](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели руин. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `desert_ruins_stone` | здание | 2x2 | 1,5 | ≤ 6 000 | да, всех |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for ancient stone ruins in a desert in harsh bright sunlight: a few low weathered walls of yellow sandstone, two broken columns without roofs, sand drifted against the bases, rubble on the ground. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `desert_ruins_stone`

```text
Ancient stone ruins about one and a half units tall: a few low weathered walls of yellow sandstone, two broken columns without roofs, sand drifted against the bases, rubble on the ground. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле.
- Стены — грубые блоки, без геометрии каждого камня.
- Блокирует всех наземных. Цвета команды нет.
- Бюджет ≤ 6 000 треугольников (предложение, крупнее средних объектов), текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `desert_ruins_stone.glb` | | | |

## Открытые вопросы

1. Блокируют ли обломки после разрушения (см. [README](README.md)).
