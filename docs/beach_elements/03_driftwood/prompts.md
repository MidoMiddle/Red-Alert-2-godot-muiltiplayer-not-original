# 03. Коряга и прибрежный мусор: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Коряга и прибрежный мусор](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей коряги и мусора. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|
| `prop_driftwood` | 1x1 | 0,5 | ≤ 800 | да |
| `prop_beach_debris` | 1x1 | 0,3 | ≤ 500 | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for driftwood and beach debris on a sandy shore in bright summer daylight: a bleached grey tree trunk with broken branches, next to a small pile of old wooden planks, a faded canister and some dark seaweed. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no legible writing.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `prop_driftwood`

```text
A bleached grey driftwood tree trunk about one unit long and half a unit tall, with broken branch stubs and smooth weathered wood, cracks along the grain. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `prop_beach_debris`

```text
A small pile of weathered wooden planks, a faded plastic canister and a few dark seaweed strands, about one unit wide and three tenths of a unit tall, no legible writing. Realistic physically based materials, clean readable silhouette, game-ready asset, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле.
- Коряга блокирует, мусор — нет. Это данные карты, в модели не отражается.
- Надписи на канистре не рисуем (язык не зависит от модели).
- Цвета команды нет.
- Бюджет: коряга ≤ 800, мусор ≤ 500 (мелкие объекты, см. [требования](../../map_elements/generation_requirements.md), раздел 3).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `prop_driftwood.glb` | | | |
| `prop_beach_debris.glb` | | | |

## Открытые вопросы

1. Нужен ли мусор как ресурс (ящики с деньгами). Сейчас нет.
