# 06. Магазин: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Магазин](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели магазина. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_shop` | здание | 2x2 | 2,5 | ≤ 6 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small shop on a city street in neutral daylight: a large display window across the front, a plain sign board above the entrance without text, a striped fabric awning over the pavement, cream walls. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_shop`

```text
A one-to-two storey shop building with a large full-width display window, a plain sign board above the entrance with no lettering, a striped fabric awning over the pavement as a separate part, cream plastered walls and a flat roof. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body` (здание), `awning` (навес, отдельно, на случай повреждения).
- **Гарнизона нет.** Витрина большая, но без комнат — игрок не ждёт бойцов (см. [appearance.md](appearance.md)).
- **Витрина** — стекло, отдельный материал `glass`, прозрачность задаём после генерации.
- **Товары в витрине** в модели не делаем: это вопрос из appearance, решаем отдельно.
- **Бюджет** ≤ 6 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_shop.glb` | Blender, скрипт `tools/blender/families/city_shop.py` | 2026-10-29 | готово |
| `city_shop_damaged.glb` | то же | 2026-10-29 | готово |
| `city_shop_abandoned.glb` | то же | 2026-10-29 | готово |
| `city_shop_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Гарнизона нет, поэтому состояний четыре: целый, повреждённый, сильно повреждённый (навес обрушен) и руины. Навес — отдельный узел `awning`. Цвета команды нет. Товаров в витрине нет, стекло запечено непрозрачным, тёплое снизу. В игре магазин — `neutral_shop` (`game/data/neutral/civilian.yaml`); модели подключены к нему в `game/data/visuals/models.yaml`.

## Открытые вопросы

1. Показывать ли товары в витрине или оставить её пустой для читаемости.
2. Должен ли магазин приносить доход владельцу. Сейчас нет, и модель от этого не меняется.
