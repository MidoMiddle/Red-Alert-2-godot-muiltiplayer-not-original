# Пси-башня — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 1×1 клетка · Роль: оборона (контроль разума)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Оборона Культа, которая не стреляет, а берёт под контроль до трёх наземных врагов. В силуэте читаются высокий шпиль и вращающиеся матрицы вокруг него: по ним видно, сколько слотов занято. Шпиль и матрицы свои; антенну оригинала не копируем.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение). В README: «1×1 (не подтверждено)»; аналог Советов — тесла-катушка, 1×1.
- **Высота:** выше танка, тонкий шпиль (предложение).
- **Подвижные части (отдельные объекты):** три матрицы вокруг шпиля — отдельные объекты, вращаются (`spin_*`; число по числу слотов, предложение). Шпиль с основанием — одна часть.
- **Узлы в Blender:** `spin_*` для матриц (соглашение, вращение по README, не сверено); `light_*` — индикатор занятого слота, по одному на слот (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на основании (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Вращающиеся элементы вокруг шпиля сохраняем: по ним игрок видит занятые слоты (от 0 до 3).
  2. Шпиль и матрицы делаем своей формы; цвет трубок выбираем по палитре фракции.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A slender defensive spire that captures enemy units and has no guns, crowned by a sensor mast, with three separate matrix rings orbiting its upper half. Taller than a tank. On a flat rectangular base plate matching a 1×1 footprint, a dark violet organic-industrial spire with a cool glowing accent. The matrix rings are separate parts. One flat panel in pure magenta #FF00FF on the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A slender defensive spire that captures enemy units and has no guns, crowned by a sensor mast, with three separate matrix rings orbiting its upper half. Taller than a tank. On a flat rectangular base plate matching a 1×1 footprint, a dark violet organic-industrial spire with a cool glowing accent. The matrix rings are separate parts. One flat panel in pure magenta #FF00FF on the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A slender defensive spire that captures enemy units and has no guns, crowned by a sensor mast, with three separate matrix rings orbiting its upper half. Taller than a tank. On a flat rectangular base plate matching a 1×1 footprint, a dark violet organic-industrial spire with a cool glowing accent. The matrix rings are separate parts. One flat panel in pure magenta #FF00FF on the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: шпиль с основанием (одна часть); три матрицы (отдельные объекты)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён в README («1×1 (не подтверждено)»). Оставляем 1×1: аналог Советов (тесла-катушка) и Союзников (призменная башня) тоже 1×1.
- Число матриц: в C&C Wiki «несколько», по README каждая держит одного пленного. Берём три (предложение, по числу слотов).
- Не подтверждено: высота, вид захвата (луч или связь на юните), вид в покое (по README матрицы вращаются; не сверено), визуал при разрушении и продаже. Визуал при повреждении — частицы `damage_*`, в модель не вписываем.
- Проверим в прототипе: читаются ли занятые слоты (0–3) с расстояния и не сливается ли башня с соседними зданиями.
