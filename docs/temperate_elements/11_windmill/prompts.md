# 11. Ветряк: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Ветряк](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели ветряка. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `temperate_windmill` | здание | 2x2 | 4 (с лопастями) | ≤ 10 000 | `body`, `roof`, `sails` | да, всех |

Крестовина с лопастями — отдельный узел `sails`: вращается в коде вокруг своей оси.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a farm windmill in warm daylight: a white plastered tower with a dark conical roof, a cross of four wooden sails with fabric stripes on the front, a door at the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `temperate_windmill`

```text
A farm windmill about four units tall: a white plastered tower with a dark conical roof, a cross of four light wooden sails with fabric stripes on the front, a dark door at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`. Крестовину отделить в узел `sails`, пивот — ось вращения.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body`, `roof`, `sails`.
- Гарнизона нет. Цвета команды нет.
- Бюджет ≤ 10 000 треугольников, текстуры 2048² (высокое здание).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `temperate_windmill.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
