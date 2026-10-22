# Большая пушка — промпты для генерации

> Фракция: Альянс (аналог Союзников), страновое здание (Франция) · Размер: 2×2 клетки (рабочее значение) · Роль: оборона (дальнобойная артиллерия)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Большая пушка — дальнобойная оборона на поворотной платформе: навесной выстрел до 15 клеток и мёртвая зона у самого орудия. В силуэте читается длинный ствол на платформе. В нашей версии облик свой: форму платформы делаем сами, турель оригинала не копируем.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** платформа около 1 единицы, ствол поднят примерно на 2 единицы (предложение; высота в источниках не описана).
- **Подвижные части (отдельные объекты):** платформа `turret` (предложение; поворот по вики), ствол `barrel` (предложение) с клипом отката `recoil` (предложение). Дёргание ствола при атаке — `barrel_jerk` (предложение; клип или узел, решить в M1).
- **Узлы в Blender:** `turret`, `barrel` с клипами `recoil` и `barrel_jerk` (предложение); `light_*` — вспышка выстрела (предложение); `damage_*` — дым и огонь (предложение); `debris_*` (предложение). Клипов `open`, `close`, `active` нет.
- **Цвет команды:** плоская панель `#FF00FF` на борту платформы (предложение: цвет игрока не подтверждён; захват невозможен).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить длинный силуэт орудия и видимый поворот ствола, чтобы дальнобойность читалась сразу; 2) форму платформы сделать своей, турель оригинала не копировать.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A long-range artillery gun emplacement that fires high-arcing shells from a rotating platform, with a long barrel. It stands on a flat rectangular base plate matching a 2×2 footprint, the platform about one unit tall with the barrel raised about two units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights on the platform. The rotating platform and the barrel are separate parts. One flat panel in pure magenta #FF00FF on the side of the platform. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A long-range artillery gun emplacement that fires high-arcing shells from a rotating platform, with a long barrel. It stands on a flat rectangular base plate matching a 2×2 footprint, the platform about one unit tall with the barrel raised about two units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights on the platform. The rotating platform and the barrel are separate parts. One flat panel in pure magenta #FF00FF on the side of the platform. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A long-range artillery gun emplacement that fires high-arcing shells from a rotating platform, with a long barrel. It stands on a flat rectangular base plate matching a 2×2 footprint, the platform about one unit tall with the barrel raised about two units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights on the platform. The rotating platform and the barrel are separate parts. One flat panel in pure magenta #FF00FF on the side of the platform. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: основание, платформа (`turret`), ствол (`barrel`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 2×2 — по порту OpenRA (рабочее значение); высота и облик по странам (appearance.md); материал и цвет игрока; дёргание ствола — в appearance.md «вики, подтверждено», в animations.md «вики, не проверено» (сверить по вики); откат ствола не подтверждён (animations.md).
- Спорные места для прототипа: читается ли дуга навесного выстрела; читается ли мёртвая зона у орудия; не делает ли медленный поворот (ROT=1) пушку беспомощной (animations.md).
