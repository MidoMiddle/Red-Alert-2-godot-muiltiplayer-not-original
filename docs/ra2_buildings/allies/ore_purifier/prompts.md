# Очиститель руды — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 3×3 клетки · Роль: экономика (бонус к доходу)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Очиститель руды — позднее экономическое здание: пассивно повышает доход от каждой разгрузки руды, сам руду не принимает. В силуэте читается компактный технический блок с вентиляцией. В нашей версии облик полностью свой: в источниках он не описан визуально.

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** около 2 единиц (предложение; в источниках не описана).
- **Подвижные части (отдельные объекты):** нет (одна часть и основание).
- **Узлы в Blender:** `light_*` — индикатор работы бонуса (предложение); `damage_*` — дым из вентиляции и огонь (предложение); `debris_*` (предложение). Клипов нет.
- **Цвет команды:** полоса `#FF00FF` на корпусе у верхней кромки фасада (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить пассивную роль: игрок видит вклад через доход, а не через картинку; 2) облик сделать полностью своим, оригинал визуально не описан.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact late-game processing annex that boosts income from ore deliveries, a sealed technical block with ventilation stacks on its roof. It stands on a flat rectangular base plate matching a 3×3 footprint, the main body about two units tall. Light brushed metal and off-white panels, tinted glass, cool blue accent lights. The ventilation stacks and status light strip are separate parts. One flat panel in pure magenta #FF00FF along the top edge of the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact late-game processing annex that boosts income from ore deliveries, a sealed technical block with ventilation stacks on its roof. It stands on a flat rectangular base plate matching a 3×3 footprint, the main body about two units tall. Light brushed metal and off-white panels, tinted glass, cool blue accent lights. The ventilation stacks and status light strip are separate parts. One flat panel in pure magenta #FF00FF along the top edge of the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact late-game processing annex that boosts income from ore deliveries, a sealed technical block with ventilation stacks on its roof. It stands on a flat rectangular base plate matching a 3×3 footprint, the main body about two units tall. Light brushed metal and off-white panels, tinted glass, cool blue accent lights. The ventilation stacks and status light strip are separate parts. One flat panel in pure magenta #FF00FF along the top edge of the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание, вентиляционные трубы и полоса индикатора (если отдельные)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: форма и высота (appearance.md); цвет игрока; Buildup по вики — не проверено (animations.md); визуального эффекта от энергии в источниках нет.
- Спорные места для прототипа: понятно ли игроку, что здание повышает доход, без подписи; не воспринимается ли пассивное здание как производственное; нужна ли вспышка индикатора при каждой разгрузке руды (animations.md).
