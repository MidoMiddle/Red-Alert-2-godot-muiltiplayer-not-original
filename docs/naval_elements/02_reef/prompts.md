# 02. Подводный риф: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Подводный риф](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели рифа. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `reef_rocks` | объект на воде | 1x1 | 1,0 | ≤ 1 000 | флот и амфибии |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for a sea reef in bright summer daylight: a cluster of dark grey rock outcrops rising just above the turquoise water, white foam breaking over them, a few dark seaweed patches at the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `reef_rocks`

```text
A cluster of dark grey sea rock outcrops about one unit across and one unit tall, rough wet surfaces, a few seaweed patches at the base, flat tops just above the waterline. Realistic physically based materials, clean readable silhouette, game-ready asset, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Пивот — центр клетки, низ на уровне дна.

## 4. Наши требования

- Узел: `body`. Пена — шейдер и частицы, в модели её нет.
- Блокирует флот и амфибий. Цвета команды нет.
- Бюджет ≤ 1 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `reef_rocks.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
