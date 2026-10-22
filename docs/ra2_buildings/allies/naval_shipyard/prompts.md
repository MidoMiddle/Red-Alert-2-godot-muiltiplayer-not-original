# Верфь — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 4×4 клетки (на воде) · Роль: производство флота и ремонт кораблей
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Верфь строит корабли и ремонтирует их в доке; ставится только на воду. В силуэте читаются бетонная платформа на воде и проём для кораблей. В нашей версии облик свой; флот вне MVP, поэтому облик можно отложить.

## Требования к этой модели

- **Размер:** 4×4 клетки = 4×4 единицы Godot (по ModEnc; стоит на воде).
- **Высота:** платформа около 0.5 единицы над водой, надстройки до 1.5 (предложение).
- **Подвижные части (отдельные объекты):** створка проёма выпуска `gate` (предложение; выпуск кораблей визуально не описан). Корабль в доке — юнит, не часть здания.
- **Узлы в Blender:** `gate` с клипами `open` и `close` (предложение, выпуск кораблей); клип `active` на доке (предложение, ремонт); `light_*` — огни дока, если будут (предложение); `damage_*` (предложение); `debris_*` (предложение).
- **Цвет команды:** плоская панель `#FF00FF` на боковине платформы у причала (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — бетон по README, не сверено в игре.
- **Особенности:** 1) сохранить то, что верфь стоит на воде: по месту здание сразу читается как морское; 2) форму платформы и причала сделать своими; флот вне MVP, облик можно отложить.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A naval construction dock that builds and repairs ships, a concrete platform on water with a bay at its side. Its flat rectangular base plate matches a 4×4 footprint, the deck half a unit above the water, the superstructure up to 1.5 units. Light brushed metal, off-white concrete-look panels, tinted glass and cool blue accent lights. The bay opening and its gate are separate parts. One panel in pure magenta #FF00FF on the platform side beside the bay. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A naval construction dock that builds and repairs ships, a concrete platform on water with a bay at its side. Its flat rectangular base plate matches a 4×4 footprint, the deck half a unit above the water, the superstructure up to 1.5 units. Light brushed metal, off-white concrete-look panels, tinted glass and cool blue accent lights. The bay opening and its gate are separate parts. One panel in pure magenta #FF00FF on the platform side beside the bay. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A naval construction dock that builds and repairs ships, a concrete platform on water with a bay at its side. Its flat rectangular base plate matches a 4×4 footprint, the deck half a unit above the water, the superstructure up to 1.5 units. Light brushed metal, off-white concrete-look panels, tinted glass and cool blue accent lights. The bay opening and its gate are separate parts. One panel in pure magenta #FF00FF on the platform side beside the bay. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единицы после нормализации
- [ ] Pivot в центре футпринта на воде, перёд −Z
- [ ] Части разделены: платформа, створка проёма (`gate`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: высота и форма платформы (appearance.md); причал, доки и краны — не подтверждены; цвет игрока не подтверждён; выпуск кораблей визуально не описан; ремонт в доке — SpecialAnim по ModEnc, узлы и стадии не подтверждены (animations.md).
- Спорные места для прототипа: читается ли корабль в доке при ремонте с игровой камеры; нужна ли анимация выпуска, если очередь уже видна в интерфейсе (предложение из animations.md).
