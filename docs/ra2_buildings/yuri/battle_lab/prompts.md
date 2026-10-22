# Боевая лаборатория — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 3×3 клетки · Роль: тех-центр (уровень 3)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Тех-центр Культа: открывает высший уровень арсенала и даёт силовое поле, которое защищает выбранные свои здания. В силуэте читаются компактный корпус с верхней платформой и кольцо света, которое загорается при активном поле. Силуэт свой.

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — Боевая лаборатория Советов, 3×3.
- **Высота:** около высоты танка, с верхней платформой (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (парящие опоры из раннего концепта в финале не подтверждены).
- **Узлы в Blender:** `active` на время поля (предложение); `light_*` — свечение кольца (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на фасаде (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Признак тех-центра — здание, открывающее высший уровень, должно выделяться на базе. Сохраняем.
  2. Силуэт делаем свой. Проверить, видно ли включённое силовое поле без лишних эффектов.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact tech center hall that unlocks the base's highest tier and projects an energy shield that protects selected friendly buildings. Roughly tank height, with a flattened crown platform. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial hall with a ring of cool glowing light along its upper edge. One flat panel in pure magenta #FF00FF on the front facade. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact tech center hall that unlocks the base's highest tier and projects an energy shield that protects selected friendly buildings. Roughly tank height, with a flattened crown platform. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial hall with a ring of cool glowing light along its upper edge. One flat panel in pure magenta #FF00FF on the front facade. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact tech center hall that unlocks the base's highest tier and projects an energy shield that protects selected friendly buildings. Roughly tank height, with a flattened crown platform. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial hall with a ring of cool glowing light along its upper edge. One flat panel in pure magenta #FF00FF on the front facade. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. Рабочее значение 3×3 по советской Боевой лаборатории (3×3).
- Парящие опоры из раннего концепта (галерея C&C Wiki) — идея, а не финальный вид. В промпт не вводим; вернуться к ним можно отдельно.
- Не подтверждено: вид силового поля, вид в покое, визуал обесточивания соседних зданий (по README поле отключает энергию базы на 60 с), визуал при повреждении, разрушении и продаже.
- Кольцо света — предложение: нужно для индикации активного поля.
- Проверим в прототипе: видно ли включённое поле и читается ли оно без лишних эффектов.
