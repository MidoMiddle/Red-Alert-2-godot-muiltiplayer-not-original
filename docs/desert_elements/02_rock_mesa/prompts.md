# 02. Скальный останец: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Скальный останец](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели останца. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `rock_mesa` | объект | 2x2 | 2,5 | ≤ 6 000 | да, всех |

Бюджет выше общего для средних объектов (≤ 3 000): останец крупный. Предложение, см. [требования](../../map_elements/generation_requirements.md), раздел 3.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for a desert rock formation in harsh bright sunlight: a tall layered sandstone pillar with ochre, red-brown and grey-yellow bands, a few boulders and scree at its base, pale sand around. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `rock_mesa`

```text
A tall layered sandstone pillar about two and a half units tall, with ochre, red-brown and grey-yellow bands, a few boulders and scree at the base. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле.
- Ярусы — текстура с полосами (не геометрия каждого яруса). Геометрия — грубые уступы.
- Блокирует всех наземных. Цвета команды нет.
- Бюджет ≤ 6 000 треугольников, текстуры 2048² (крупный объект).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `rock_mesa.glb` | | | |

## Открытые вопросы

1. Нужна ли вторая модель с обломанным верхом (см. [README](README.md)).
