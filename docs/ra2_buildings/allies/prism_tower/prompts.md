# Призменная башня — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 1×1 клетка (рабочее значение) · Роль: оборона (против наземных целей)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Призменная башня — продвинутая оборона Альянса: тонкая башня с вращающимися зеркалами, которые копят энергию и дают луч. В силуэте читаются тонкий корпус и шесть зеркал на верхушке. В нашей версии облик свой: цвет луча и форма зеркал наши; цвет луча в источниках не описан.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение: по README, appearance.md пишет «не подтверждено источниками»).
- **Высота:** тонкая башня около 3 единиц с зеркалами наверху (предложение).
- **Подвижные части (отдельные объекты):** зеркала `spin_mirrors` — отдельный объект (предложение, по соглашению `spin_*`); вращение в покое не подтверждено.
- **Узлы в Blender:** `spin_mirrors`, `light_core` — свет ядра при заряде (предложение), `beam_main` — луч выстрела (предложение), `beam_support_*` — лучи поддержки (предложение), узел точки выхода луча — аналог FLH (предложение), `damage_*` (предложение), `debris_*` (предложение).
- **Цвет команды:** плоская полоса `#FF00FF` на корпусе у основания (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты не подтверждены; сталь по README — броня.
- **Особенности:** 1) сохранить видимую зарядку и связь между башнями: без них группа не читается как усиленный залп; 2) цвет луча и форму зеркал сделать своими; «белый жар» у призменного танка из источников — лишь один вариант.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A slender energy tower that charges a focused beam, its top crowned by six rotating mirrors around a glowing charge core. It stands on a flat rectangular base plate matching a 1×1 footprint, the tower about three units tall. Light brushed metal and off-white panels, tinted glass mirror faces and cool blue glow in the core. The rotating mirrors and the charge core are separate parts. One flat strip in pure magenta #FF00FF on the tower body near its base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A slender energy tower that charges a focused beam, its top crowned by six rotating mirrors around a glowing charge core. It stands on a flat rectangular base plate matching a 1×1 footprint, the tower about three units tall. Light brushed metal and off-white panels, tinted glass mirror faces and cool blue glow in the core. The rotating mirrors and the charge core are separate parts. One flat strip in pure magenta #FF00FF on the tower body near its base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A slender energy tower that charges a focused beam, its top crowned by six rotating mirrors around a glowing charge core. It stands on a flat rectangular base plate matching a 1×1 footprint, the tower about three units tall. Light brushed metal and off-white panels, tinted glass mirror faces and cool blue glow in the core. The rotating mirrors and the charge core are separate parts. One flat strip in pure magenta #FF00FF on the tower body near its base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, зеркала (`spin_mirrors`), ядро (`light_core`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 1×1 (appearance.md — не подтверждено источниками); высота; цвет луча башни не описан (у призменного танка — «белый раскалённый», вики, косвенно); анимация зарядки — галерея, вид не описан.
- Расхождение статуса (сверить по галерее): неиспользуемую анимацию разрушения appearance.md помечает «подтверждено подписью», animations.md — «вики, не проверено».
- Спорные места для прототипа: видно ли, какая башня стреляет и какие её поддерживают, при 3 и при 5 башнях; читается ли разница между одиночным выстрелом и залпом группы.
