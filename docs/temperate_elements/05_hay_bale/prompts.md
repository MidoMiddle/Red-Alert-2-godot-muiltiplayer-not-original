# 05. Стог сена: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Стог сена](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели тюка сена. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `hay_bale` | объект | 1x1 | 1,0 | ≤ 1 000 | да, всех |

Горение — шейдер и эффекты в Godot. Обгоревший вариант — тот же тюк с чёрным шейдером (отдельная модель не нужна).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a round hay bale in a field in warm daylight: golden straw wrapped in light brown twine, loose straw around the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `hay_bale`

```text
A round hay bale about one unit tall, golden straw wrapped in light brown twine, loose straw around the base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле.
- Блокирует всех наземных. Цвета команды нет.
- Сено — текстура с соломой, без отдельной геометрии каждой соломинки.
- Бюджет ≤ 1 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `hay_bale.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
