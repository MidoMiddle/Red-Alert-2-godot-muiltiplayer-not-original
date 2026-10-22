# Очистительный завод — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 4×3 клетки · Роль: экономика
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Очистительный завод превращает руду в кредиты: харвестер сдаёт груз в приёмную яму внутри здания. В силуэте читаются широкий корпус и приёмник руды. В нашей версии облик свой: светлый металл и синий акцент; путь харвестера и приёмник — наши решения.

## Требования к этой модели

- **Размер:** 4×3 клетки = 4×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** корпус около 2 единиц (предложение; силуэт в источниках не описан).
- **Подвижные части (отдельные объекты):** ворота выезда бесплатного харвестера `gate` (предложение; выезд в источниках не подтверждён). Приёмная яма неподвижна.
- **Узлы в Blender:** `ore_pit` — пустой узел для частиц руды при разгрузке (предложение). `gate` с клипами `open` и `close` (предложение). `damage_*` — дым и искры (предложение).
- **Цвет команды:** плоская панель `#FF00FF` на фасаде над приёмной ямой (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить видимую точку разгрузки — яму или проём, чтобы игрок видел, куда сдаётся руда; 2) форму приёмника и путь харвестера сделать своими; эффект телепорта заменить короткой вспышкой.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
An ore-processing plant that turns ore into credits, a wide building with a recessed ore intake pit on its front face. It stands on a flat rectangular base plate matching a 4×3 footprint, its main body about two units tall. Light brushed metal and off-white panels, tinted glass windows, cool blue accent lights. A wide gate sits beside the intake pit as a separate part. One flat panel in pure magenta #FF00FF on the front wall above the intake pit. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
An ore-processing plant that turns ore into credits, a wide building with a recessed ore intake pit on its front face. It stands on a flat rectangular base plate matching a 4×3 footprint, its main body about two units tall. Light brushed metal and off-white panels, tinted glass windows, cool blue accent lights. A wide gate sits beside the intake pit as a separate part. One flat panel in pure magenta #FF00FF on the front wall above the intake pit. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
An ore-processing plant that turns ore into credits, a wide building with a recessed ore intake pit on its front face. It stands on a flat rectangular base plate matching a 4×3 footprint, its main body about two units tall. Light brushed metal and off-white panels, tinted glass windows, cool blue accent lights. A wide gate sits beside the intake pit as a separate part. One flat panel in pure magenta #FF00FF on the front wall above the intake pit. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, ворота выезда (`gate`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 4×3 — по порту OpenRA (рабочее значение); высота и трубы не описаны (appearance.md); ворота выезда по README, в источниках не описаны; цвет игрока; тип и вид анимации ссыпания руды.
- Расхождение (сверить в игре): appearance.md (раздел «Анимация») пишет, что по ModEnc отдельной анимации ссыпания нет; animations.md по ModEnc (SpecialAnim) считает, что анимация разгрузки есть. Файлы не правлю.
- Спорные места для прототипа: виден ли момент сдачи руды; нужна ли зависимость анимации от заполнения хранилища (animations.md).
