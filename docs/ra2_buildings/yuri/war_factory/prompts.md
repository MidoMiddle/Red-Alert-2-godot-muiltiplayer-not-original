# Военный завод — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 5×3 клетки · Роль: производство (техника и авиация)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Производство техники и авиации Культа: широкий ангар с раздвижной крышей. При вылете летающего юнита крыша раскрывается, и игрок видит, откуда он появился. В силуэте читаются крыша и выездная створка; палитра и способ раскрытия свои.

## Требования к этой модели

- **Размер:** 5×3 клетки = 5×3 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — Военный завод Советов, 5×3 (один ряд — выезд).
- **Высота:** около высоты танка у стен (предложение).
- **Подвижные части (отдельные объекты):** раздвижные панели крыши (открываются при вылете авиации); створка выезда наземной техники — если дверь анимируется (предложение).
- **Узлы в Blender:** `open` и `close` для крыши и створки выезда (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на передней стене (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Раскрытие крыши при вылете авиации сохраняем: игрок видит, откуда взялся юнит.
  2. Способ раскрытия и палитру делаем свои. Проверить, не закрывает ли открытая крыша юниты на земле.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A wide vehicle and aircraft production hall with sliding roof panels that open to launch flying units, and a ground exit door on one short side. Roughly tank height at the walls. On a flat rectangular base plate matching a 5×3 footprint, a dark violet organic-industrial shell with a cool glowing accent along the roof edges. The sliding roof panels and the exit door leaf are separate parts. One flat panel in pure magenta #FF00FF on the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A wide vehicle and aircraft production hall with sliding roof panels that open to launch flying units, and a ground exit door on one short side. Roughly tank height at the walls. On a flat rectangular base plate matching a 5×3 footprint, a dark violet organic-industrial shell with a cool glowing accent along the roof edges. The sliding roof panels and the exit door leaf are separate parts. One flat panel in pure magenta #FF00FF on the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A wide vehicle and aircraft production hall with sliding roof panels that open to launch flying units, and a ground exit door on one short side. Roughly tank height at the walls. On a flat rectangular base plate matching a 5×3 footprint, a dark violet organic-industrial shell with a cool glowing accent along the roof edges. The sliding roof panels and the exit door leaf are separate parts. One flat panel in pure magenta #FF00FF on the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 5×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием, раздвижные панели крыши, створка выезда
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. Рабочее значение 5×3 по советскому аналогу, где один ряд клеток занят выездом.
- Расхождение источников: `appearance.md` описывает раздвижную крышу (C&C Wiki), `animations.md` пишет, что ключи ModEnc (`RoofDeployingAnim`, `UnderRoofDoorAnim`) описывают смену изображения, а не движение. Таблица `War_Factory_Door_Image` и ключи ModEnc противоречат друг другу. Не решено, что именно движется.
- Не подтверждено: форма корпуса и высота, палитра, вид в покое, визуал выезда наземной техники (`DeployingAnim`, `UnderDoorAnim` по ModEnc, не сверены), поведение без энергии.
- Проверим в прототипе: не закрывает ли открытая крыша юниты; порядок слоёв (крыша и проём под ней).
