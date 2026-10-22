# 04. Морская нефтяная платформа: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Морская нефтяная платформа](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели платформы. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `offshore_platform` | здание на воде | 2x2 | 4 | ≤ 8 000 | `body`, `derrick`, `team_mask` | да (полоса на перилах) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for an offshore oil platform in bright summer daylight: a square white deck on four thick legs standing in the sea, a lattice derrick and a pump on the deck, a dark rusty base in the water. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `offshore_platform`

```text
An offshore oil platform about four units tall: a square white deck on four thick legs standing in the sea, a lattice derrick and a small pump on the deck, a narrow plain colour band on the deck railing, rust at the waterline. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`. Пивот — центр футпринта на уровне воды.

## 4. Наши требования

- Узлы: `body` (палуба и ноги), `derrick` (вышка), `team_mask` (полоса на перилах).
- Насос — текстура или отдельный узел; качание — код.
- Блокирует флот и амфибий. Пламя и дым — частицы.
- Текстуры 2048² (большое техздание), бюджет ≤ 8 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `offshore_platform.glb` | | | |

## Открытые вопросы

1. Нужна ли вторая модель для обломанной платформы (см. [README](README.md)).
