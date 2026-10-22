# Верфь — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 4×4 клеток · Роль: производство (флот), ремонт кораблей
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Бетонная платформа 4×4, которая строится только на воде: кран и док над ней показывают, что это морской завод для флота и ремонта кораблей. Признак «здание на воде» читается по платформе и доку, по ним видна морская база. Кран и док делаем в собственном стиле, описание README перед закреплением проверяем в прототипе.

## Требования к этой модели

- **Размер:** 4×4 клеток = 4×4 единиц Godot (строится только на воде).
- **Высота:** палуба примерно в одну высоту танка, кран примерно в три (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание; кран и док названы в README, но их движение не подтверждено (`animations.md`, «Простой»).
- **Узлы в Blender:** шейдер `build_up` (постройка и продажа); `damage_*` на кране, доке и платформе (места — предложение); `debris_*` при разрушении (предложение); клипы `open`, `active`, `close` под три стадии ремонта и метка `repair_cell` на клетке дока (предложение).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стенке платформы (предложение; цвет игрока на верфи не подтверждён, `appearance.md`).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сохранить признак «здание на воде» — платформу и док; кран и док сделать в собственном стиле.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A large concrete harbour platform for building and repairing ships, built only on water, with a gantry crane and a dock, the deck about one tank high and the crane about three tanks tall. Standing on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. The crane and dock are fixed parts of the platform. One flat panel in pure magenta #FF00FF on a side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A large concrete harbour platform for building and repairing ships, built only on water, with a gantry crane and a dock, the deck about one tank high and the crane about three tanks tall. Standing on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. The crane and dock are fixed parts of the platform. One flat panel in pure magenta #FF00FF on a side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A large concrete harbour platform for building and repairing ships, built only on water, with a gantry crane and a dock, the deck about one tank high and the crane about three tanks tall. Standing on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. The crane and dock are fixed parts of the platform. One flat panel in pure magenta #FF00FF on a side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Кран и док: названы в README, но их вид и движение не описаны (`appearance.md`, «Материалы и цвета»; `animations.md`, «Простой»). Поэтому в модели они неподвижны.
  - Выход кораблей из верфи и производственная анимация не описаны; клип выхода не закладываем (`animations.md`, «Производство и выход кораблей»).
  - Что показывают стадии ремонта и как они соотносятся с клипами `open`, `active`, `close`, не описано; соответствие — предложение (`animations.md`, «Ремонт кораблей в доке»).
  - Вид при повреждении, разрушении и продаже не описан; судьба корабля в доке при гибели здания не подтверждена (`appearance.md`, «Состояния»; `animations.md`, «Повреждение и разрушение»).
  - Высота относительно юнитов не подтверждена (`appearance.md`, «Силуэт и размер»); значения в промпте — предложение.
- Спорные места, которые проверим в прототипе:
  - Нужны ли все три стадии ремонта или хватит `active` (`animations.md`, «Для нашей версии», пункт 1).
  - Как шейдер `build_up` выглядит на воде и принимает ли здание точку сбора во время постройки (`animations.md`, «Для нашей версии», пункт 2).
  - Как цикл `active` прерывается без денег и как сочетается с огнём `damage_*` (`animations.md`, «Для нашей версии», пункт 3).
  - Видна ли платформа на светлой и тёмной воде и не путается ли она с пристанью (`appearance.md`, «Для нашей версии»).
  - Нужна ли верфи зависимость от энергии: в YR её убрали, у нас предложено так же (README, «Заметки для нашего проекта»; `animations.md`, «Без энергии»).
