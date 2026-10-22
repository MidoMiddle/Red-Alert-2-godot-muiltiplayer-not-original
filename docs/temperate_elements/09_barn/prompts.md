# 09. Амбар: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Амбар](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели амбара. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `temperate_barn` | здание | 3x2 | 3 | ≤ 8 000 | `body`, `roof`, `gate` | да, всех |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a large wooden barn in warm daylight: dark red weathered planks, a grey gabled roof, a wide double gate on the front, small ventilation windows with light frames. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `temperate_barn`

```text
A large wooden barn about three units long, two units wide and three units tall: weathered dark red planks, a grey gabled roof, a wide double gate on the front, small ventilation windows with light frames. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`. Ворота — отдельный узел `gate`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body`, `roof`, `gate` (створка ворот, открывается в коде).
- Гарнизона нет. Цвета команды нет.
- Бюджет ≤ 8 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `temperate_barn.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
