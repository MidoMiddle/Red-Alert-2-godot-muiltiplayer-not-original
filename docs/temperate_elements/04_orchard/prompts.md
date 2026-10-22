# 04. Фруктовый сад: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Фруктовый сад](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей яблонь. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Варианты |
|---|---|---|---|---|
| `tree_apple_01`, `tree_apple_02` | 1x1 | 1,6 | ≤ 2 000 | два силуэта |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for apple trees in an orchard in warm daylight: two low round-crowned apple trees in a row, green leaves with a few red apples, whitewashed lower trunks. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `tree_apple_01`

```text
An apple tree about one and a half units tall: a low round crown of green leaves with a few red apples, a short trunk with a white-painted base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `tree_apple_02`

Взять промт 3.1 и добавить в конец: `The crown is slightly taller and has no apples.`

## 4. Наши требования

- Пивот — центр клетки на земле.
- Плоды — маленькие сферы в геометрии или текстура листвы с точками.
- Побелка — текстура ствола.
- Цвета команды нет.
- Бюджет ≤ 2 000 треугольников (LOD0), LOD1 около 50 %.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `tree_apple_01.glb` | | | |
| `tree_apple_02.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
