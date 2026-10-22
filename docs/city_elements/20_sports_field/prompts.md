# 20. Спортплощадка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Спортплощадка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса, моделей и текстур спортплощадки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_sports_goal` | объект | 1x1 | 2 | ≤ 500 | нет | нет |
| `city_sport_grass` | текстура, бесшовная | — | — | — | — | — |
| `city_sport_lines` | декаль с прозрачностью | — | — | — | — | — |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small city football pitch in neutral daylight: a rectangular grass field with white painted lines, two white goal frames with netting at the ends, and a low grey wire-mesh fence around it. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated scene on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель и текстуры

### 3.1. `city_sports_goal` (модель, Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

```text
A white metal football goal frame about one unit wide and two units tall, with a net on the back, two posts and a crossbar, slightly weathered paint. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `city_sport_grass` (текстура, генератор изображений)

```text
Seamless tileable texture of a mown football pitch grass with faint darker stripes from mowing and a few worn patches, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.3. `city_sport_lines` (декаль, генератор изображений)

```text
Pitch markings decal on a transparent background: white painted lines of a football pitch, a centre line, a centre circle and penalty boxes, thin and even, square image, no text.
```

Фон вырезаем после генерации.

## 4. Наши требования

- **Модель стойки:** пивот — центр клетки на земле. Узлы: `frame`, `net`. Цвета команды нет.
- **Бюджет** ≤ 500 треугольников, текстуры 1024².
- **Поле — не модель.** Геометрии у поля нет: это текстура и декаль разметки.
- **Не блокирует** — это данные карты, в модели не отражается.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_sports_goal.glb` | | | |
| `city_sport_grass.png` | | | |
| `city_sport_lines.png` | | | |

## Открытые вопросы

1. Нужна ли асфальтовая версия текстуры (см. [appearance](appearance.md)).
