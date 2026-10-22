# Радарная вышка — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 2×2 клеток · Роль: информация, второй тех-уровень
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Небольшая вышка 2×2 с двумя антенными тарелками на мачте: издалека видно, что здесь ведётся разведка. Тарелки вращаются, пока есть питание, поэтому состояние читается без интерфейса. Форма тарелок и корпуса у нас своя, не копия оригинала.

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot.
- **Высота:** мачта примерно в два раза выше танка (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** две тарелки антенн, узлы `spin_dish_0..N`, по одному на тарелку, как в `animations.md`.
- **Узлы в Blender:** `spin_dish_0..N` (вращение тарелок); `damage_*` (лёгкий дым до 50% HP, огонь и густой дым до 25% HP; предложение); шейдер `build_up` (постройка и продажа).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стороне основания под тарелками (предложение; цвет игрока на здании не подтверждён, `appearance.md`).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сохранить вращающиеся тарелки как признак работы радара (читается без интерфейса); число и форму тарелок сделать своими (в промпте две).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A slender sensor tower for battlefield scouting, about twice the height of a tank, with two antenna dishes on a short mast. Standing on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. The two dishes are separate rotating parts on the mast. One flat panel in pure magenta #FF00FF on the side of the base block below the dishes. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A slender sensor tower for battlefield scouting, about twice the height of a tank, with two antenna dishes on a short mast. Standing on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. The two dishes are separate rotating parts on the mast. One flat panel in pure magenta #FF00FF on the side of the base block below the dishes. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A slender sensor tower for battlefield scouting, about twice the height of a tank, with two antenna dishes on a short mast. Standing on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. The two dishes are separate rotating parts on the mast. One flat panel in pure magenta #FF00FF on the side of the base block below the dishes. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: `spin_dish_0..N` (две тарелки), корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Высота относительно юнитов: в README её нет, в `appearance.md` она помечена как неподтверждённая. «Примерно в два раза выше танка» — предложение.
  - Число тарелок: в галерее C&C Wiki две, в альфа-версии три (`appearance.md`). В промпте две.
  - Вид самолёта-разведчика (YR) и анимация здания при его вызове не описаны (`appearance.md`, `animations.md`, «Действия»).
  - Направление, скорость и период вращения тарелок не подтверждены (`animations.md`, «Простой», «Действия»).
  - Кадры, стадии и длительность `Buildup` (постройка и продажа) не подтверждены (`animations.md`, «Постройка»).
  - Цвет игрока на здании не подтверждён (`appearance.md`): панель `#FF00FF` выбрана как предложение.
- Спорные места, которые проверим в прототипе:
  - Видна ли остановка вращения при нехватке энергии с дальнего плана (`appearance.md`, «Для нашей версии»).
  - Нужен ли сигнал на здании при вызове разведчика и при проникновении шпиона (`animations.md`, «Для нашей версии», пункт 3).
  - Порог сильного повреждения 25% — проектный; страница ModEnc `ConditionRed` называет 50% по умолчанию (`animations.md`, «Повреждение и разрушение»).
