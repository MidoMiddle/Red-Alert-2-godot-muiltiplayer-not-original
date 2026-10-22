# 04. Пляжный зонтик: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пляжный зонтик](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели зонтика. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|
| `beach_umbrella` | 1x1 | 2,2 | ≤ 800 | `pole`, `canopy` | нет |

Расцветки — варианты текстуры тента, одна модель.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a beach umbrella in bright summer daylight: a round striped canopy in red and white on a thin metal pole stuck in the sand, a dark underside of the canopy. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_umbrella`

```text
A beach umbrella: a round striped canopy about one unit across with a dark underside, on a thin metal pole about two and a fifth units tall, stuck into the sand. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `pole` (столб), `canopy` (тент, покачивается в коде).
- Тент — одна геометрия, полосы — текстура.
- Цвета расцветок — варианты текстуры, не новые модели.
- Блокирования нет: тонкий столб.
- Бюджет ≤ 800 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_umbrella.glb` | | | |

## Открытые вопросы

1. Нужны ли разные формы тента (круглый и квадратный).
