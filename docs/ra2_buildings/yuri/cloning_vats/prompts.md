# Клонирующие чаны — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 2×2 клетки · Роль: производство (клонирование пехоты)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Здание, которое создаёт бесплатную копию каждого пехотинца, обученного в казармах. В силуэте читаются две высокие подсвеченные ёмкости: по ним игрок понимает, что здесь что-то появляется. Форма ёмкостей и палитра свои, графика из RA2 не переносится.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — Клонирующие чаны Советов, 2×2 (в RA2 это было советское здание).
- **Высота:** выше танка (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (в источниках подвижных частей нет).
- **Узлы в Blender:** `active` на момент появления копии (предложение); `light_*` — короткая вспышка (предложение, идея из `appearance.md`); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на корпусе между ёмкостями (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Признак «появился клон» — короткая анимация копии у здания — сохраняем: иначе бесплатный дубликат незаметен.
  2. Форму ёмкостей и палитру делаем свои. Проверить, не путается ли копия с основным юнитом.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
Two tall translucent incubation vats joined by a shared housing, each with a cool glowing liquid core. The vats create a second copy of every infantry unit trained in the base. Taller than a tank. On a flat rectangular base plate matching a 2×2 footprint, a dark violet organic-industrial base with a cool glowing accent. One flat panel in pure magenta #FF00FF on the housing between the vats. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
Two tall translucent incubation vats joined by a shared housing, each with a cool glowing liquid core. The vats create a second copy of every infantry unit trained in the base. Taller than a tank. On a flat rectangular base plate matching a 2×2 footprint, a dark violet organic-industrial base with a cool glowing accent. One flat panel in pure magenta #FF00FF on the housing between the vats. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
Two tall translucent incubation vats joined by a shared housing, each with a cool glowing liquid core. The vats create a second copy of every infantry unit trained in the base. Taller than a tank. On a flat rectangular base plate matching a 2×2 footprint, a dark violet organic-industrial base with a cool glowing accent. One flat panel in pure magenta #FF00FF on the housing between the vats. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием и ёмкостями (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Происхождение.** В RA2 это здание было советским (README, `appearance.md`). В YR его передали Юрию; в нашей версии это здание Культа. Форма, графика и палитра свои.
- **Число ёмкостей.** Две — предложение. В источниках форма и число ёмкостей не описаны.
- Размер не подтверждён; рабочее значение 2×2 по советскому аналогу (те же Клонирующие чаны, 2×2).
- **Расхождение источников.** `appearance.md`: иконка и анимации версии Юрия отличаются от советской (галерея C&C Wiki). `animations.md` ссылается на cncnz, где сказано, что чаны в YR «mostly unchanged». Не решено; проверить по галерее, когда она станет доступна.
- Не подтверждено: вид в покое, вид в момент клонирования, визуал при повреждении, разрушении и продаже. По ModEnc чаны работают и при нехватке энергии; визуал этого состояния не описан.
- Проверим в прототипе: виден ли момент клонирования на масштабе карты.
