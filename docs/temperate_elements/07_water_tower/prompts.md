# 07. Водонапорная башня: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Водонапорная башня](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели башни. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `water_tower` | здание | 1x1 | 4 | ≤ 4 000 | `legs`, `tank` | да, основание |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a steel water tower on the edge of a farm village in warm daylight: a round tank on four tall legs with cross bracing, a ladder up one leg, rust streaks on the metal. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `water_tower`

```text
A steel water tower about four units tall: a round tank on four tall legs with cross bracing, a ladder up one leg, grey-blue metal with rust streaks. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind building --footprint 1x1`. Пивот — центр основания на земле.

## 4. Наши требования

- Узлы: `legs` (ноги и связи), `tank` (бак, для визуала пробоины).
- Блокирует одну клетку основания, бак над головой — визуально.
- Цвета команды нет.
- Бюджет ≤ 4 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `water_tower.glb` | | | |

## Открытые вопросы

1. Гарнизон для башни (см. [README](README.md)).
