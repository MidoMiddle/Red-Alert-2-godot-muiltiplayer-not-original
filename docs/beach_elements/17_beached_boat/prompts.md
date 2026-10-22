# 17. Лодка на песке: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Лодка на песке](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели лодки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `beached_boat` | объект | 1x1 | 0,6 | ≤ 1 500 | `hull`, `oars` | да |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a small wooden fishing boat pulled up onto the sand in bright summer daylight: a blue painted hull lying on its side, a long oar beside it, a coil of rope on the sand. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beached_boat`

```text
A small wooden fishing boat with a pointed bow and a flat stern, painted blue, lying on its side on the sand, about one unit long and six tenths of a unit tall, with an oar lying beside it. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `hull` (корпус, именование как у техники, M1 4.7), `oars` (весло).
- Блокирует одну клетку (корпус). Цвета корпуса — варианты текстуры.
- Цвета команды нет.
- Бюджет ≤ 1 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beached_boat.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
