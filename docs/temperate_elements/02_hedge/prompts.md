# 02. Живая изгородь: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Живая изгородь](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели секции изгороди. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|
| `hedge_section` | 1x1 (одна секция) | 0,8 | ≤ 500 | да, всех |

Углы и концы делаем в Blender из секции, как у заборов ([08_fences_walls](../../map_elements/08_fences_walls/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a clipped living hedge along a farm field in warm daylight: a straight dense row of dark green shrubs with flat sides and a flat top, about one unit tall, grass at the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `hedge_section`

```text
A straight clipped hedge section one unit long and eight tenths of a unit tall, dense dark green foliage with flat sides and a flat top, a few brown twigs at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Листву делаем геометрией с низкой плотностью, при необходимости — альфа-карта.

## 4. Наши требования

- Пивот — центр секции на земле. Секции стыкуются по краю клетки.
- Блокирует всех наземных. Цвет команды нет.
- Бюджет ≤ 500 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `hedge_section.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
