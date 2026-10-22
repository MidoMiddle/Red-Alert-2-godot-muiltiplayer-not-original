# 06. Оазис: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Оазис](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели тростника. Пальмы и вода — из других наборов. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует | Источник |
|---|---|---|---|---|---|
| `desert_reeds` | 1x1 | 1,0 | ≤ 800 | нет | новая модель |
| `tree_palm_01`…`_03` | 1x1 | 3 | ≤ 3 000 | да | [пляжный набор](../../beach_elements/02_palm/prompts.md), переиспользуем |
| вода | — | — | — | по типу | [03_water](../../map_elements/03_water/prompts.md), шейдер |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for a desert oasis in harsh bright sunlight: a small turquoise pond with a dark centre, tall green reeds at the edge, a few palms around it, pale sand all around. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `desert_reeds`

```text
A clump of tall green reeds about one unit tall, slender stems with long leaves and a few brown seed heads. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Листья — геометрия низкой плотности или альфа-карта.

## 4. Наши требования

- Тростник не блокирует, пальмы блокируют (как в [02_palm](../../beach_elements/02_palm/README.md)).
- Вода — шейдер и тип клеток, в модели её нет.
- Цвета команды нет.
- Бюджет тростника ≤ 800 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `desert_reeds.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
