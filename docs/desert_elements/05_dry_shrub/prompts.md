# 05. Сухой кустарник и перекати-поле: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Сухой кустарник и перекати-поле](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей кустарника и перекати-поля. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|
| `desert_dry_shrub` | 1x1 | 0,5 | ≤ 500 | нет |
| `tumbleweed` | 1x1 | 0,6 | ≤ 500 | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for dry desert shrubs and a tumbleweed in harsh bright sunlight: grey-brown dead twigs of a small shrub growing between stones, and a round ball of dry tangled twigs lying on pale sand. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `desert_dry_shrub`

```text
A small dry desert shrub about half a unit tall and one unit wide, grey-brown dead twigs in a spread-out bush shape, no leaves. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, harsh bright desert sunlight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `tumbleweed`

```text
A tumbleweed: a round ball about six tenths of a unit across made of dry tangled grey-brown twigs, loosely woven. Realistic physically based materials, clean readable silhouette, game-ready asset, harsh bright desert sunlight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Катится в коде, вращения в модели нет.

## 4. Наши требования

- Пивот — центр клетки на земле.
- Не блокируют проход. Цвета команды нет.
- Перекати-поле — один узел `ball`, вращается в коде по ветру.
- Бюджет ≤ 500 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `desert_dry_shrub.glb` | | | |
| `tumbleweed.glb` | | | |

## Открытые вопросы

Открытых вопросов по моделям нет.
