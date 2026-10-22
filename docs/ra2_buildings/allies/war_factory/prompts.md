# Военный завод — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 5×3 клетки · Роль: производство (техника)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Военный завод выпускает всю наземную технику; это самое длинное здание базы. В силуэте читаются широкий корпус, ворота выезда и раскрывающаяся крыша. В нашей версии облик свой: светлый металл и синий акцент, форма ворот и крыши наша.

## Требования к этой модели

- **Размер:** 5×3 клетки = 5×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено; по README — самое длинное здание Альянса).
- **Высота:** корпус около 2.5 единицы (предложение; силуэт в источниках не описан).
- **Подвижные части (отдельные объекты):** ворота выезда техники `gate` (предложение; по README выезд через ворота) и крыша `roof` (предложение; по вики раскрывается для авиации). Два отдельных объекта.
- **Узлы в Blender:** `gate` и `roof`, каждый с клипами `open` и `close` (предложение; два отдельных узла в одном AnimationPlayer). `damage_*` — дым и огонь у ворот (предложение).
- **Цвет команды:** широкая полоса `#FF00FF` над воротами на фасаде (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить понятный выход техники (ворота или створки), чтобы игрок видел, откуда выезжают танки; 2) форму крыши и ворот сделать своими; проверить, не возникает ли пробки у выхода.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A heavy production factory that builds all ground machines, a long industrial hall with a wide gate on its front and a retractable roof above the rear bay. It stands on a flat rectangular base plate matching a 5×3 footprint, about two and a half units tall. Light brushed metal panels, off-white trim, tinted glass and cool blue accent lights. The front gate and the roof section are separate parts. One broad strip in pure magenta #FF00FF above the front gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A heavy production factory that builds all ground machines, a long industrial hall with a wide gate on its front and a retractable roof above the rear bay. It stands on a flat rectangular base plate matching a 5×3 footprint, about two and a half units tall. Light brushed metal panels, off-white trim, tinted glass and cool blue accent lights. The front gate and the roof section are separate parts. One broad strip in pure magenta #FF00FF above the front gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A heavy production factory that builds all ground machines, a long industrial hall with a wide gate on its front and a retractable roof above the rear bay. It stands on a flat rectangular base plate matching a 5×3 footprint, about two and a half units tall. Light brushed metal panels, off-white trim, tinted glass and cool blue accent lights. The front gate and the roof section are separate parts. One broad strip in pure magenta #FF00FF above the front gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 5×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, ворота (`gate`), крыша (`roof`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 5×3 — по порту OpenRA (рабочее значение); ворота выезда по README, в источниках не описаны; форма и облик крыши по вики не описаны.
- Расхождение статуса (сверить по вики): appearance.md помечает раскрывающуюся крышу как «вики, подтверждено», animations.md — как «вики, не проверено».
- Спорные места для прототипа: не мешают ли открытые ворота выезду техники; видна ли раскрытая крыша при вылете авиации (авиация вне MVP); нужна ли пауза между `open` и выездом техники (animations.md).
