# 03. Льдина: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Льдина](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели льдины. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — зимний климат: холодный свет, снег, без национальных символов. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `ice_floe` | объект на воде | 1x1 | 0,2 | ≤ 800 | флот |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for a floating ice floe on a winter sea: a flat irregular slab of pale blue ice with cracks, a thin layer of snow on top, clear ice edges above dark grey water. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `ice_floe`

```text
A flat irregular floating ice floe about one unit across and a fifth of a unit thick above the water, pale blue ice with fine cracks, a thin layer of snow on top, clear ice edges. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Пивот — центр клетки на уровне воды.

## 4. Наши требования

- Узел: `body`. Трещины — текстура. Покачивание — код.
- Блокирует флот. Цвета команды нет.
- Бюджет ≤ 800 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `ice_floe.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
