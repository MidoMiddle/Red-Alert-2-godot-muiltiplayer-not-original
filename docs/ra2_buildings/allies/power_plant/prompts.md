# Электростанция — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 2×2 клетки · Роль: энергия
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Электростанция — единственный источник энергии базы: компактная газотурбинная установка с теплообменниками. В силуэте читаются невысокий корпус и турбинный блок на крыше; здание узнаётся по компактности. В нашей версии облик свой: светлый металл и синий акцент, форму оборудования делаем сами.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot.
- **Высота:** корпус около 1.5 единицы (предложение).
- **Подвижные части (отдельные объекты):** ротор турбины (предложение: `spin_turbine`). Других подвижных частей в animations.md нет.
- **Узлы в Blender:** `spin_turbine` (предложение), `smoke_*` — дым вытяжки (предложение), `damage_*` — дым и искры (предложение). `light_*` не закладываем, пока не подтверждено (animations.md). Клипов нет.
- **Цвет команды:** одна плоская полоса `#FF00FF` на верхней кромке фасада (предложение: цвет игрока на здании не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить компактный силуэт и роль источника энергии, чтобы здание узнавалось на карте сразу; 2) форму оборудования сделать своей: читаемость по силуэту важнее точной копии турбины.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact gas-turbine power generator building that supplies energy to the base. Its hall is about one and a half units tall, with three heat-exchanger drums on the roof. It stands on a flat rectangular base plate matching a 2×2 footprint. Light brushed metal housings, off-white panels, tinted glass and cool blue accent lights. The turbine rotor is a separate part. One flat panel in pure magenta #FF00FF along the upper edge of the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact gas-turbine power generator building that supplies energy to the base. Its hall is about one and a half units tall, with three heat-exchanger drums on the roof. It stands on a flat rectangular base plate matching a 2×2 footprint. Light brushed metal housings, off-white panels, tinted glass and cool blue accent lights. The turbine rotor is a separate part. One flat panel in pure magenta #FF00FF along the upper edge of the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact gas-turbine power generator building that supplies energy to the base. Its hall is about one and a half units tall, with three heat-exchanger drums on the roof. It stands on a flat rectangular base plate matching a 2×2 footprint. Light brushed metal housings, off-white panels, tinted glass and cool blue accent lights. The turbine rotor is a separate part. One flat panel in pure magenta #FF00FF along the upper edge of the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, ротор турбины
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: высота (appearance.md); три теплообменника — по вики, визуально не сверено; огни и антенны не подтверждены; цвет игрока не подтверждён; вращение турбины и дым в источниках не описаны.
- Расхождение (сверить): стоимость — $800 в README, cncnz.com по animations.md даёт $600. На модель не влияет.
- Спорные места для прототипа: виден ли признак повреждения и падения выработки на силуэте; нужно ли вращать турбину, если вращение в оригинале не подтверждено.
