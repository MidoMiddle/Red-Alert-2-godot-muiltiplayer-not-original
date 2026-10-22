# 08. Пляжный бар: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пляжный бар](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели бара. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `beach_bar` | здание | 3x2 | 3 | ≤ 8 000 | `body`, `roof`, `counter` | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for an open-air beach bar on sand in bright summer daylight: a light wooden counter, a striped fabric awning on dark wooden posts, a few small tables and chairs outside, a plain sign board without text. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_bar`

```text
An open-air beach bar: a light wooden counter, a striped fabric awning on dark wooden posts over it, a few small tables and chairs outside, a plain sign board with no text, about three units long and three units tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body` (стойка и опоры), `roof` (навес), `counter` (стойка для блокирования).
- Столики и стулья — отдельные мелкие объекты или часть модели: в модели только для читаемости.
- Гарнизона нет.
- Полоса навеса — текстура (варианты расцветки).
- Бюджет ≤ 8 000 треугольников, текстуры 2048² (крупное здание).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_bar.glb` | | | |

## Открытые вопросы

1. Нужен ли бару отдельный вариант с закрытой стойкой и без навеса (зимой или в городе).
