# 01. Рельеф: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Рельеф](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и текстур этого элемента. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Цвет команды |
|---|---|---|---|
| `cliff_rock` | текстура, бесшовная | 1024² | нет |
| `scree` | текстура, бесшовная | 1024² | нет |

3D-модели нет. Рельеф строится по сетке высот, обрывы — меш из кода (M7). Поэтому Meshy и Tripo здесь не нужны.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/relief_cliff/`.

```text
Concept art reference for a real-time strategy game terrain feature: a steep rocky cliff face in a snowy winter landscape, dark grey-brown layered stone with thin brown bands, snow ledges on top, and a scree slope of loose gravel and snow at the foot. Three-quarter view from 45 degrees, looking down at about 30 degrees. Soft overcast light, realistic materials, square image, no people, no text, no logos.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `cliff_rock`

```text
Seamless tileable texture of a dark grey-brown rock cliff face with layered sediment, thin brown bands, small cracks and old snow in the crevices. Photorealistic, top-down orthographic view, even lighting with no shadows and no highlights, 1024x1024, no text.
```

### 3.2. `scree`

```text
Seamless tileable texture of loose scree: grey and brown gravel and small stones mixed with patches of snow. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстуры **бесшовные по обеим осям**: обрыв накладывается триплоскостной проекцией (M7, 4.12), поэтому UV-координат у текстуры нет.
- Карта нормалей для `cliff_rock` получается из серой карты высот, которую просим у генератора отдельно. Формат OpenGL.
- Масштаб тайлинга подбираем в Godot по виду, а не в генераторе.
- Снег на склоне и на вершине обрыва пока не отдельная текстура: снег даёт слой `snow_clean` из [02_land_types](../02_land_types/prompts.md).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `cliff_rock.png` | | | |
| `scree.png` | | | |

## Открытые вопросы

1. Нужна ли отдельная текстура снега на осыпи, или хватит смешивания в шейдере.
