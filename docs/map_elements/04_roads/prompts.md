# 04. Дороги: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Дороги](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и текстур дорог. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Когда нужен |
|---|---|---|---|
| `road_packed_snow` | текстура, бесшовная | 1024² | зима (MVP) |
| `road_asphalt` | текстура, бесшовная | 1024² | город (позже) |

3D-моделей нет. Дорога — канал B маски (M7, M1 4.4). Разметка — декали, пешеходный переход — в [18 у городских элементов](../../city_elements/18_pedestrian_crossing/README.md).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/roads/`.

```text
Concept art reference for a winter road between snowbanks: a packed snow road with two dark tyre tracks running along it, low snowbanks on both sides, a wooden fence post at the edge. Three-quarter view from 45 degrees, looking down at about 30 degrees. Soft cold light, realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `road_packed_snow`

```text
Seamless tileable texture of a packed snow road surface with two darker wheel track grooves running lengthwise, compacted and slightly icy. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `road_asphalt`

```text
Seamless tileable texture of worn grey asphalt with fine aggregate and small cracks, no painted lines. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Колеи — часть текстуры `road_packed_snow`. Они должны идти вдоль дороги, а не поперёк, иначе дорога будет выглядеть как поле.
- Ширина дороги — одна клетка. Текстура должна тайлиться без видимого шва на краю клетки.
- Бонус скорости колёсной технике задаёт данные (M7), текстура его не показывает.
- Цвет команды не нужен.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `road_packed_snow.png` | | | |
| `road_asphalt.png` | | | |

## Открытые вопросы

1. Нужна ли текстура грунтовки для умеренного климата (позже, в M11).
