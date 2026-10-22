# 01. Песок и дюны: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Песок и дюны](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры песка. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `beach_sand` | текстура, бесшовная | 1024² | сухой песок, тип «берег» |
| `beach_wet_sand` | текстура, бесшовная | 1024² | влажный песок у воды (вариант того же типа) |

Модели нет: дюны — рельеф из кода (по сетке высот).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a sandy beach with dunes in bright summer daylight: pale sand with wind ripples, a ridge of dunes with soft shadows, a darker wet strip of sand near the water. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `beach_sand`

```text
Seamless tileable texture of dry pale beach sand with fine wind ripples, a warm yellow-beige tone, tiny shell fragments. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `beach_wet_sand`

```text
Seamless tileable texture of wet dark sand with a glossy sheen and small pebbles, grey-brown tone. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстуры бесшовные, 1024², PNG, без запечённых теней.
- Тип земли один (`b`), текстуры — только визуал. В маске песок — слой остатка, как снег в зимнем климате (M1, 4.4).
- Масштаб тайлинга подбираем в Godot.
- Цвет команды не нужен.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_sand.png` | | | |
| `beach_wet_sand.png` | | | |

## Открытые вопросы

1. Нужна ли отдельная текстура сухой травы для дюн (сейчас — декаль поверх песка).
