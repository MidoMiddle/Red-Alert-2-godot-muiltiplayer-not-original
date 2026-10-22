# Электростанция — промпты для генерации

> Фракция: нейтральное · Размер: 2×2 клеток · Роль: энергия (+200 при захвате)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Гражданская электростанция 2×2 с трубами на корпусе: при захвате даёт владельцу +200 энергии. В силуэте читаются трубы и корпус; дым из труб — эффект в узлах `smoke_*`, в модель не входит. В нашей версии станция должна отличаться гражданским стилем от станций сторон (appearance.md).

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot (рабочее значение: по README; по источникам не сверено, см. appearance.md).
- **Высота:** корпус около высоты танка, трубы примерно вдвое выше танка (предложение). Высота относительно юнитов не подтверждена.
- **Подвижные части (отдельные объекты):** одна часть и основание. Движущихся частей нет (animations.md).
- **Узлы в Blender:** `smoke_*` у труб, только при работе (предложение); `light_*` на фасаде как индикатор работы (предложение); `damage_*` с дымом, искрами и огнём (предложения). Клипов нет.
- **Цвет команды:** одна плоская панель `#FF00FF` на стене у входа (предложение: цвет игрока в источниках не подтверждён).
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение).
- **Особенности:**
  - Сохранить роль «даёт энергию» и видимый маркер нейтрального захватываемого здания (appearance.md).
  - Сделать своим гражданский стиль, отличный от станций сторон, чтобы его было проще различать (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A civilian city power station, a compact brick and concrete generating plant, on a flat rectangular base plate matching a 2×2 footprint. Its main hall is about the height of a battle tank, and two chimney stacks rise from the roof, about twice the tank height. A side transformer block sits beside the hall. One flat panel in pure magenta #FF00FF on the wall beside the entrance. Neutral civilian industrial look, no faction accents, no smoke. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A civilian city power station, a compact brick and concrete generating plant, on a flat rectangular base plate matching a 2×2 footprint. Its main hall is about the height of a battle tank, and two chimney stacks rise from the roof, about twice the tank height. A side transformer block sits beside the hall. One flat panel in pure magenta #FF00FF on the wall beside the entrance. Neutral civilian industrial look, no faction accents, no smoke. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, smoke, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A civilian city power station, a compact brick and concrete generating plant, on a flat rectangular base plate matching a 2×2 footprint. Its main hall is about the height of a battle tank, and two chimney stacks rise from the roof, about twice the tank height. A side transformer block sits beside the hall. One flat panel in pure magenta #FF00FF on the wall beside the entrance. Neutral civilian industrial look, no faction accents, no smoke. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: вид станции (трубы по README, в игре не сверено; их число, охлаждение и ограждение не подтверждены); вид при дефиците энергии и при повреждении (appearance.md); цвет игрока; размер 2×2 по источникам не сверен.
- Спорные места, которые проверим в прототипе: видно ли игроку, что станция повреждена и выработка падает; нужен ли визуальный сигнал, что при захвате энергия подключилась к сети.
