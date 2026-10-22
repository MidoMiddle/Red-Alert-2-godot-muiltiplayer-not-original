# Ремонтный цех — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 3×3 клетки · Роль: поддержка (ремонт техники)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Ремонтный цех — площадка, на которую заезжает повреждённая техника и чинится за кредиты; без него не строится MCV. В силуэте должны читаться площадка и низкая рама над ней. В нашей версии облик свой: светлый металл и синий акцент; способ показать ремонт (огни, пар) наш.

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** площадка около 0.5 единицы, рама до 2 (предложение; «плоская площадка» из README в источниках не подтверждена).
- **Подвижные части:** нет (одна часть и основание).
- **Узлы в Blender:** `light_*` — индикатор готовности и ремонта (предложение); `smoke_*` — пар при ремонте (предложение); `damage_*` — дым и огонь (предложение); `debris_*` (предложение). Клипов `open`, `close`, `active` нет.
- **Цвет команды:** полоса `#FF00FF` по краю площадки (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить видимый знак ремонта, чтобы техника на площадке читалась как чинящаяся, а не просто стоящая; 2) способ показать ремонт (полоса, искры, открытый люк) сделать своим, без копирования анимаций оригинала.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A repair pad, a flat platform where damaged machines are repaired, with a low gantry frame over one end. It stands on a flat rectangular base plate matching a 3×3 footprint, the pad about half a unit high and the frame up to two units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The gantry frame and its status lights are separate parts. One flat strip in pure magenta #FF00FF along the edge of the pad. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A repair pad, a flat platform where damaged machines are repaired, with a low gantry frame over one end. It stands on a flat rectangular base plate matching a 3×3 footprint, the pad about half a unit high and the frame up to two units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The gantry frame and its status lights are separate parts. One flat strip in pure magenta #FF00FF along the edge of the pad. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A repair pad, a flat platform where damaged machines are repaired, with a low gantry frame over one end. It stands on a flat rectangular base plate matching a 3×3 footprint, the pad about half a unit high and the frame up to two units. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The gantry frame and its status lights are separate parts. One flat strip in pure magenta #FF00FF along the edge of the pad. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание (подвижных частей нет)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 3×3 — по порту OpenRA (рабочее значение); форма площадки (appearance.md); вид IdleAnim и SpecialAnim не описан; цвет игрока не подтверждён.
- Расхождение (сверить): appearance.md пишет, что ремонт запускает три специальные анимации (ModEnc, «подтверждено»). animations.md: ModEnc описывает SpecialAnim только для первой стадии, число три не сверено.
- Спорные места для прототипа: читается ли ремонт на площадке без подписи; нужна ли вторая стадия ремонта (animations.md).
