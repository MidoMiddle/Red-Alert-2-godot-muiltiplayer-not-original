# 04. Кактус: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Кактус](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей кактуса. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Варианты |
|---|---|---|---|---|
| `cactus_01` … `cactus_03` | 1x1 | 1,5 | ≤ 1 000 | высокий, низкий, с ветвями |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for desert cacti in harsh bright sunlight: a tall green columnar cactus with dark spines, a low round cactus, and a branched cactus with two arms, on pale sand. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `cactus_01`

```text
A tall green columnar desert cactus about one and a half units tall, dark spines in rows along the ribs, a rounded top. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `cactus_02`, `cactus_03`

Взять промт 3.1 и заменить форму:
- `_02`: `A low round cactus, about one unit tall, with short dense spines.`
- `_03`: `A branched cactus with two arms bent upwards.`

## 4. Наши требования

- Пивот — центр клетки на земле.
- Колючки — геометрия или текстура ребер (что дешевле по бюджету).
- Блокирует всех наземных. Цвета команды нет.
- Бюджет ≤ 1 000 треугольников (LOD0), текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `cactus_01.glb` | | | |
| `cactus_02.glb` | | | |
| `cactus_03.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
