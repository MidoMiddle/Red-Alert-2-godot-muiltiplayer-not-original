# 11. Верблюды и стервятники: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Верблюды и стервятники](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса, модели верблюда и спрайта стервятника. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня». Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `civ_camel` | животное, 3D | 1x1 | 1,2 | ≤ 2 000 | нет |
| `vulture_sprite` | 2D-спрайт, с прозрачностью | 128² | — | — | нет |

Скелет верблюда — авториг генератора или упрощённый вручную (Mixamo для животных не подходит, см. [13_livestock](../../temperate_elements/13_livestock/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for camels and a vulture in a desert in harsh bright sunlight: a sandy camel with one hump standing at a small oasis, a dark vulture with bare head soaring in the sky above. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text, no logos.
```

## 3. Модели и спрайты

### 3.1. `civ_camel` (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

```text
A dromedary camel about one unit long and one and a fifth units tall, one hump, sandy brown coat with a lighter belly, standing relaxed on four legs. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated figure on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `vulture_sprite` (генератор изображений)

```text
Flat side-view sprite of a soaring vulture with wings spread, dark brown plumage, bare head, on a transparent background, sharp edges, 128x128, no text.
```

Фон вырезаем после генерации. Спрайт — не 3D: в воздухе, на земле не блокирует.

## 4. Наши требования

- Верблюд: пивот — центр клетки на земле, не блокирует проход. Узлы: `body`, `head`, `legs`.
- Стервятник — спрайт в небе, не модель.
- Цвета команды нет.
- Бюджет верблюда ≤ 2 000 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_camel.glb` | | | |
| `vulture_sprite.png` | | | |

## Открытые вопросы

Открытых вопросов по моделям нет.
