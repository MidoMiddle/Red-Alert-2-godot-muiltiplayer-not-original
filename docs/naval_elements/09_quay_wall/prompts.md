# 09. Причальная стенка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Причальная стенка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры бетонной стенки. Моделей нет: стенка — рельеф. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `quay_concrete` | текстура, бесшовная | 1024² | бетонная стенка (триплоскостная проекция, как обрывы) |
| `quay_edge`, `quay_edge_plain`, `quay_edge_damaged` | объект на краю стенки | 1x1 | отбойник, тумбы, кранцы; трап у `quay_edge` |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for a concrete quay wall at a harbour in bright summer daylight: a flat light grey concrete wall above the water, rusty mooring rings set into it, worn steps down to the sea, dark algae at the waterline. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text, no logos.
```

## 3. Текстура (генератор изображений, не 3D)

### 3.1. `quay_concrete`

```text
Seamless tileable texture of light grey concrete panels with thin dark joints, small chips and weathering, faint algae at the bottom edge. Photorealistic, orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстура бесшовная по обеим осям: стенка накладывается триплоскостной проекцией, как обрывы ([M7](../../implementation/M7_maps.md), раздел 4.12).
- Кольца и ступени — отдельные мелкие модели (решено 2026-11-01): край причала `quay_edge` с тумбами, кранцами и трапом, без трапа — `quay_edge_plain`. Узел `body`, бюджет 1 500 треугольников, текстура 1024².
- Цвета команды нет.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `quay_concrete.png` | Blender 5.2.3 + numpy, вручную (скрипт `tools/blender/families/ground_textures.py`) | 2026-11-01 | сделано |
| `quay_edge.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_quay_edge.py`) | 2026-11-01 | сделано, 1 028 треугольников |
| `quay_edge_plain.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_quay_edge.py`) | 2026-11-01 | сделано, 800 треугольников |
| `quay_edge_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_quay_edge.py`) | 2026-11-01 | сделано, 984 треугольника |

## Открытые вопросы

Открытых вопросов по текстуре нет.
