# 09. Каменная арка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Каменная арка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели арки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `rock_arch` | объект | 2x1 | 2,2 | ≤ 6 000 | да, всех |

Бюджет выше общего для средних объектов: арка крупная (предложение).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for a natural sandstone arch in a desert in harsh bright sunlight: two thick layered pillars with a flat top spanning over a passage, ochre and red-brown bands, sand and small stones at the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `rock_arch`

```text
A natural sandstone arch about two units tall and two units long: two thick layered pillars with a flat top spanning over a passage, ochre and red-brown bands, sand and small stones at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body` (арка), `base` (основание из щебня).
- Блокирует всех наземных. Цвета команды нет.
- Ярусы — текстура полос, геометрия — грубые уступы.
- Бюджет ≤ 6 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `rock_arch.glb` | | | |

## Открытые вопросы

1. Проход под аркой (см. [README](README.md)).
