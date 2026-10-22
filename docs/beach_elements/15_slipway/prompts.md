# 15. Слип: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Слип](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели слипа. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `slipway` | спуск | 2x1 | 0,5 | ≤ 2 000 | `ramp` | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a concrete boat slipway on a beach in bright summer daylight: a wide ribbed concrete ramp sloping down from the sand into shallow water, rusty metal edges, a few mooring rings at the top. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `slipway`

```text
A concrete boat slipway: a wide ribbed concrete ramp about two units long that slopes down from the sand into the water, rusty metal edges, two mooring rings at the top. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`. Угол склона совпадает с высотами карты: модель не должна задавать наклон сама (см. [README](README.md)).

## 4. Наши требования

- Пивот — центр футпринта на земле, низ спуска — на уровне воды. Узел: `ramp`.
- Наклон модели — только визуальный, геометрию склона задают высоты карты.
- Не блокирует проход.
- Цвета команды нет.
- Бюджет ≤ 2 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `slipway.glb` | | | |

## Открытые вопросы

1. Нужна ли отдельная модель, если склон задан высотами (см. [README](README.md)).
