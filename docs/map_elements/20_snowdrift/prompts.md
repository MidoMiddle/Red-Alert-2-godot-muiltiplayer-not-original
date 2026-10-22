# 20. Сугроб: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Сугроб](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей сугробов. Общие правила — в [требованиях](../generation_requirements.md). Стиль — зимний климат: холодный дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `snowdrift_01` | объект | 1x1 | 0,4 | ≤ 500 | нет |
| `snowdrift_02` | объект | 1x1 | 0,3 | ≤ 500 | нет |

Снег поля — текстура из [02_land_types](../02_land_types/prompts.md) (`snow_clean`). Сугроб — отдельная модель с тем же снегом.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/winter/`.

```text
Concept art reference for a winter snowdrift in a field: a smooth wind-shaped heap of snow with a sharp ridge on one side and a gentle slope on the other, soft blue shadows, one tyre track through part of it. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral cold daylight, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `snowdrift_01`

```text
A wind-shaped snowdrift about one unit across and four tenths of a unit tall: a smooth white heap of snow with a sharp curved ridge on one side and a gentle slope on the other, soft blue-grey shadows. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `snowdrift_02`

Взять промт 3.1 и заменить форму: `A low, long snowdrift about one unit long and three tenths of a unit tall, with two parallel ridges.`

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `body` (горка). Колея — декаль, не геометрия.
- Блокирует нет. Цвета команды нет.
- Снег — материал с высокой шероховатостью, без блеска.
- Бюджет ≤ 500 треугольников (мелкий объект, см. [требования](../generation_requirements.md), раздел 3), текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `snowdrift_01.glb` | | | |
| `snowdrift_02.glb` | | | |

## Открытые вопросы

1. Колея — декаль или часть текстуры поля (см. [appearance](appearance.md)).
