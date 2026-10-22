# 16. Волнорез: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Волнорез](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели блока волнореза. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `breakwater_block` | блок | 1x1 | 0,8 | ≤ 1 000 | `body` | да, всех |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a row of large granite breakwater blocks in the sea in bright summer daylight: rough cut grey stone blocks half above the water, dark seaweed at the waterline, white foam against the blocks. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `breakwater_block`

```text
A single large grey granite breakwater block about one unit wide and eight tenths of a unit tall, rough cut faces, slightly rounded edges, dark seaweed at the bottom, a few light chips on top. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки, низ блока на уровне воды. Узел: `body`.
- Блок — массивный, блокирует всё на воде. Цвета команды нет.
- Текстура камня — общая с валуном из [09_props](../../map_elements/09_props/prompts.md), если подходит.
- Бюджет ≤ 1 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `breakwater_block.glb` | | | |

## Открытые вопросы

1. Разрушается ли блок обычным оружием или только тяжёлым (см. [README](README.md)).
