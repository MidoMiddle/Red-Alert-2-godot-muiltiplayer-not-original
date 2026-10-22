# 04. Трубопровод: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Трубопровод](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `pipeline_straight` | мелкий объект | 1x1 | 0,6 | ≤ 1 000 | нет | нет (нейтральный) |
| `pipeline_corner` | мелкий объект | 1x1 | 0,6 | ≤ 1 000 | нет | нет (нейтральный) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/industrial/`.

```text
Concept art reference for an industrial pipeline on low steel supports: a grey-green metal pipe about half a metre thick, flanged joints, a right-angle bend, concrete footings under the supports. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `pipeline_straight`

```text
A straight industrial steel pipe segment about one unit long and 0.6 units tall, on two low steel supports with concrete footings, flanged joints at both ends. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated object on a plain light grey background.
```

### 3.2. `pipeline_corner`

Взять промт 3.1 и заменить форму: `A right-angle bend of an industrial steel pipe, one unit by one unit, the same supports and flanges.`

Нормализация: `--kind prop --footprint 1x1` для обоих.

## 4. Наши требования

- Концы трубы — на границах клетки, чтобы сегменты стыковались без зазора.
- Узлы: `pipe` (труба), `support` (опоры), `flange` (фланцы).
- Пивот в центре клетки на земле, высота 0,6.
- Текстуры 1024² (мелкий объект). Бюджет ≤ 1 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `pipeline_straight.glb` | | | |
| `pipeline_corner.glb` | | | |

## Открытые вопросы

1. Нужен ли третий вариант, тройник (T-образный сегмент).
