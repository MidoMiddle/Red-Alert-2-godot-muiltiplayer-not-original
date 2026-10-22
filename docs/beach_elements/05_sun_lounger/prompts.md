# 05. Шезлонг: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Шезлонг](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели шезлонга. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|
| `beach_sun_lounger` | 1x1 | 0,6 | ≤ 500 | `body` | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a beach sun lounger in bright summer daylight: a low lounger with a reclined striped fabric back on four short legs, light metal frame. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_sun_lounger`

```text
A beach sun lounger: a light metal frame on four short legs, a reclined back and seat covered in striped fabric, about one unit long and six tenths of a unit tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узел `body`.
- Не блокирует проход.
- Цвет ткани — варианты текстуры.
- Бюджет ≤ 500 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_sun_lounger.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
