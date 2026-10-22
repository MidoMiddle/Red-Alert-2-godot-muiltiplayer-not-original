# 03. Каньон: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Каньон](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстур каньона. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

Модели нет: стены — обрывы из рельефа, дно — склоны. Нужны две текстуры.

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `canyon_wall` | текстура, бесшовная | 1024² | ярусы стен (триплоскостная проекция, как обрывы) |
| `canyon_floor` | текстура, бесшовная | 1024² | дно: песок и галька |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for a desert canyon in harsh bright sunlight: steep walls of layered red, ochre and grey-yellow rock, a sandy and pebbly floor between the walls, deep shadow at the bottom. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `canyon_wall`

```text
Seamless tileable texture of a layered canyon rock wall with horizontal bands of red, ochre and grey-yellow sandstone, small cracks. Photorealistic, orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `canyon_floor`

```text
Seamless tileable texture of pale sand and pebbles on the floor of a dry canyon, small dry grass tufts. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстура стен — триплоскостная проекция: тайлится по обеим осям, UV не нужен (как у обрывов, [M7](../../implementation/M7_maps.md), раздел 4.12).
- Дно — тип «бездорожье», текстура — только визуал.
- Цвета команды нет.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `canyon_wall.png` | | | |
| `canyon_floor.png` | | | |

## Открытые вопросы

Открытых вопросов по текстурам нет.
