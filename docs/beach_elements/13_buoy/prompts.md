# 13. Буй: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Буй](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели буя. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `prop_buoy` | объект на воде | 1x1 | 0,8 | ≤ 500 | `body`, `light` | нет |

Огонёк — emissive в Godot, в модели погашен.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a navigation buoy floating on calm sea water in bright summer daylight: a bright red conical body with a thin metal mast and an unlit small lamp on top, a little foam around the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `prop_buoy`

```text
A floating navigation buoy: a bright red conical body about one unit wide and eight tenths of a unit tall, a thin grey metal mast on top with a small unlit lamp. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки, низ корпуса на уровне воды. Узлы: `body`, `light` (огонёк, emissive в Godot).
- Покачивание — код, не анимация модели.
- Не блокирует ни пехоту, ни флот.
- Цвета команды нет.
- Бюджет ≤ 500 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `prop_buoy.glb` | | | |

## Открытые вопросы

1. Навигационная метка для флота (см. [README](README.md)).
