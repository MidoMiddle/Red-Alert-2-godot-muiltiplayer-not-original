# 03. Вода и берег: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Вода и берег](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса, текстур льда и воды. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `frozen_water` | текстура, бесшовная | 1024² | замёрзшая река и озёра |
| `water_height` | карта высот, серая, бесшовная | 1024² | вода: рябь, из неё делаем карту нормалей |

3D-моделей нет. Цвет открытой воды, глубина и пена у берега — шейдер (M7, 4.5), текстура им не нужна.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/water/`.

```text
Concept art reference for a frozen river at dusk: a long stretch of thick grey-white ice with fine cracks and a narrow strip of dark open water in the middle, snow on the banks, a few bare bushes at the shore. Three-quarter view from 45 degrees, looking down at about 30 degrees. Soft cold light, realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `frozen_water`

```text
Seamless tileable texture of thick river ice with fine cracks and a light snow dusting, pale blue-white with subtle depth. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `water_height`

```text
Seamless tileable grayscale height map of gentle small water ripples, black and white only, no color, top-down view, 1024x1024, no text.
```

Из этой картинки делаем карту нормалей в Blender или GIMP, формат OpenGL.

## 4. Наши требования

- Лёд `frozen_water` — основной вид зимних водоёмов. Полыньи — отдельная зона (цвет открытой воды из шейдера, M7, 4.5).
- Нормали — OpenGL-формат, как в Godot.
- Вода всегда ровная: её высота равна высоте углов клетки (см. [README](README.md) и [01_relief](../01_relief/prompts.md)).
- Цвет команды не нужен.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `frozen_water.png` | | | |
| `water_height.png` | | | |
| `water_normal.png` (из `water_height`) | | | |

## Открытые вопросы

1. Нужны ли трещины во льду от тяжёлой техники как текстура или только как эффект.
