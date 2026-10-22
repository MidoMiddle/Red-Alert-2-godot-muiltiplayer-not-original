# 19. Рыбацкие сети: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Рыбацкие сети](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели сетей. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `fishing_nets` | объект | 2x1 | 1,5 | ≤ 800 | `poles`, `net` | нет |

Сеть — тонкая геометрия: как у листвы и волейбольной сетки, допускаем альфа-карту ([09_volleyball_net](../09_volleyball_net/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for fishing nets drying on wooden poles on a sandy beach in bright summer daylight: a brown-grey net stretched between two pale weathered poles, small dark floats along the top edge, a few weights along the bottom. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `fishing_nets`

```text
Fishing nets drying on two weathered wooden poles about one and a half units tall, a brown-grey net stretched between them with small dark floats along the top edge. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`. Сеть в Blender отделить узлом `net` (альфа-карта).

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `poles`, `net`.
- Не блокирует проход.
- Цвета команды нет.
- Бюджет ≤ 800 треугольников, текстуры сети 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `fishing_nets.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
