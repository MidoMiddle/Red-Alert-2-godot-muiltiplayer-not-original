# 18. Сарай для лодок: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Сарай для лодок](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели сарая. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `beach_boat_shed` | здание | 2x2 | 2,5 | ≤ 6 000 | `body`, `roof`, `door` | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a wooden boat shed on a beach in bright summer daylight: a faded blue plank building with a gabled grey roof, large double doors facing the water, nets and oars hanging on the outside wall. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_boat_shed`

```text
A wooden boat shed on a beach: faded blue planked walls, a gabled grey roof, large double doors facing the water, about two units wide and two and a half units tall. No people, no lettering. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body` (стены), `roof`, `door` (двустворчатые двери, могут открываться в коде).
- Гарнизона нет. Окон с комнатами внутри нет.
- Цвет команды нет.
- Бюджет ≤ 6 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_boat_shed.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
