# 09. Волейбольная сетка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Волейбольная сетка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели сетки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `beach_volleyball_net` | объект | 2x1 | 1,0 | ≤ 800 | `posts`, `net` | нет |

Сетка — тонкая прозрачная геометрия: как в [элементах карты](../../map_elements/07_trees/prompts.md) листва, допускаем альфа-карту, если геометрия не укладывается в бюджет.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a beach volleyball net on sand in bright summer daylight: two metal posts at the ends of a court with a white mesh net stretched between them, white court lines painted on the sand. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_volleyball_net`

```text
A beach volleyball net: two grey metal posts about one unit tall at the ends of a court, a white mesh net stretched between them. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`. Сетку в Blender сделать отдельным узлом `net` (альфа-карта, если геометрия тяжёлая).

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `posts`, `net`.
- Не блокирует проход.
- Линии площадки — декаль на песке, не модель.
- Цвета команды нет.
- Бюджет ≤ 800 треугольников; текстуры сетки 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_volleyball_net.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
