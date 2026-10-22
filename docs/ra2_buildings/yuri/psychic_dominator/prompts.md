# Пси-доминатор — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 3×3 клетки · Роль: супероружие (атакующее)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Главное супероружие Культа: огромный излучатель, который при зарядке раскрывает крышки и поднимает центральный элемент над ними. В силуэте читается раскрытое или закрытое устройство, то есть визуальный таймер готовности. Механизм подъёма и силуэт свои, без копирования оригинальной «руки».

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — Железный занавес Советов, 3×3, и ядерная шахта Советов, 3×3.
- **Высота:** заметно выше остальных зданий Культа, выше танка (предложение).
- **Подвижные части (отдельные объекты):** две крышки и центральный подъёмный элемент — отдельные объекты (предложение). Корпус с основанием — одна часть.
- **Узлы в Blender:** `open` для раскрытия при зарядке, `active` для выстрела, `close` для закрытия (предложение); `light_*` — прогресс заряда (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на кромке основания (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Два состояния — закрыт и раскрыт при зарядке — сохраняем как визуальный таймер готовности.
  2. Механизм подъёма и силуэт делаем свои. Проверить, видна ли зона захвата при выстреле.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A colossal superweapon emitter, the tallest structure of the base, with two hinged covers and a central crown element that lifts on a column above them, shown with the covers closed. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial body with a cool glowing accent around the emitter opening. The two covers and the crown element are separate parts. One flat panel in pure magenta #FF00FF on the base plate edge. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A colossal superweapon emitter, the tallest structure of the base, with two hinged covers and a central crown element that lifts on a column above them, shown with the covers closed. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial body with a cool glowing accent around the emitter opening. The two covers and the crown element are separate parts. One flat panel in pure magenta #FF00FF on the base plate edge. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A colossal superweapon emitter, the tallest structure of the base, with two hinged covers and a central crown element that lifts on a column above them, shown with the covers closed. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial body with a cool glowing accent around the emitter opening. The two covers and the crown element are separate parts. One flat panel in pure magenta #FF00FF on the base plate edge. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием (одна часть); две крышки и центральный элемент (отдельные объекты)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. Рабочее значение 3×3 по советским аналогам (Железный занавес и ядерная шахта, оба 3×3).
- **Механизм подъёма.** В C&C Wiki верхняя конструкция поднимается, как «рука кукловода». `appearance.md` предлагает свой механизм. Силуэт в промпте описан своими словами.
- Не подтверждено: форма и высота, вид в покое (закрытый вариант Inactive описан в галерее, не сверен), вид выстрела (ключ `DominatorFirstAnim` в ModEnc не описан), момент урона (`DominatorFireAtPercentage`), визуал без энергии, при повреждении, разрушении и продаже.
- Референс — закрытый вариант (Inactive, по `animations.md`). Открытие делаем клипом `open` (предложение).
- Проверим в прототипе: видна ли зона захвата (3×3 по README) при выстреле; читается ли заряд с расстояния.
