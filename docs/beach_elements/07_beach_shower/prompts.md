# 07. Пляжный душ: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пляжный душ](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели душа. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|
| `beach_shower` | 1x1 | 2,2 | ≤ 500 | `pipe`, `head` | нет |

Вода — частицы в Godot, в модели её нет.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a beach outdoor shower in bright summer daylight: a tall grey metal pipe with a curved showerhead on top, standing on a light concrete slab with a dark drain grate. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_shower`

```text
A beach outdoor shower: a tall grey metal pipe about two and a fifth units tall with a curved showerhead on top, on a light concrete slab with a dark drain grate. No water in the model. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `pipe` (труба), `head` (лейка; вода из неё — частицы).
- Не блокирует проход.
- Цвет команды нет.
- Бюджет ≤ 500 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_shower.glb` | | | |

## Открытые вопросы

1. Нужна ли решётка как отдельный узел (проходимая часть площадки), если она будет влиять на следы.
