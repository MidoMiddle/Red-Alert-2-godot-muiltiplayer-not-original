# Спутник-шпион — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 2×2 клетки · Роль: информация (полная разведка)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Станция связи со спутником — небольшое здание с тарелкой-антенной, которое снимает чёрную завесу с карты для владельца. В силуэте читается тарелка на мачте. В нашей версии облик свой: светлая тарелка и синие огни; тарелку из README стоит сверить в игре, прежде чем брать её как образец.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** основание около 1 единицы, тарелка на мачте до 2.5 (предложение).
- **Подвижные части (отдельные объекты):** тарелка с мачтой `spin_dish` (предложение; тарелка по README, не сверена в игре).
- **Узлы в Blender:** `spin_dish` (предложение), `light_*` — индикатор работы (предложение), `damage_*` — дым и искры (предложение), `debris_*` (предложение). Клипов нет.
- **Цвет команды:** плоская панель `#FF00FF` на боковине основания (предложение: цвет игрока на здании не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — не подтверждена.
- **Особенности:** 1) сохранить понятный знак «карта раскрыта», чтобы игрок видел, что разведка работает; 2) облик антенны сделать своим.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A satellite ground-station building that gives its owner full map reconnaissance: a small housing with a parabolic dish antenna on a mast. It stands on a flat rectangular base plate matching a 2×2 footprint, the base about one unit high, the dish up to 2.5 units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The dish and its mast are separate parts that rotate. One flat panel in pure magenta #FF00FF on the side of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A satellite ground-station building that gives its owner full map reconnaissance: a small housing with a parabolic dish antenna on a mast. It stands on a flat rectangular base plate matching a 2×2 footprint, the base about one unit high, the dish up to 2.5 units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The dish and its mast are separate parts that rotate. One flat panel in pure magenta #FF00FF on the side of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A satellite ground-station building that gives its owner full map reconnaissance: a small housing with a parabolic dish antenna on a mast. It stands on a flat rectangular base plate matching a 2×2 footprint, the base about one unit high, the dish up to 2.5 units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The dish and its mast are separate parts that rotate. One flat panel in pure magenta #FF00FF on the side of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: основание, тарелка с мачтой (`spin_dish`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 2×2 — по порту OpenRA (рабочее значение); тарелка — по README, не сверено в игре (appearance.md); визуального индикатора раскрытия на здании источники не описывают; цвет игрока не подтверждён; вращение — предложение (animations.md).
- Расхождение (сверить): стоимость — $1500 в README, cncnz.com по animations.md даёт $1000.
- Спорные места для прототипа: заметно ли, что карта раскрыта, и не путают ли здание с радаром Командования ВВС; зависит ли раскрытие от питания (README: по ModEnc не проверяет).
