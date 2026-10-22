# Установка контроля погоды — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 3×3 клетки · Роль: супероружие (буря)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Установка контроля погоды — атакующее супероружие: высокая башня-излучатель вызывает грозовую бурю над выбранной точкой. В силуэте читается высокая башня; сама буря — отдельный эффект на поле, а не часть модели. В нашей версии облик свой; предупреждение перед ударом важно для читаемости.

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** башня-излучатель около 4.5 единицы на основании (предложение; форма башни в источниках не описана).
- **Подвижные части (отдельные объекты):** нет (одна часть и основание).
- **Узлы в Blender:** `light_emitter` — свечение излучателя на время заряда и бури (предложение); `damage_*` — трещины, дым, искры (предложение); `debris_*` (предложение). Буря — отдельный VFX на поле (предложение). `spin_*` не назначаем.
- **Цвет команды:** плоская панель `#FF00FF` на основании башни (предложение: цвет игрока не подтверждён; захват невозможен).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — бетон по README, не сверено в игре.
- **Особенности:** 1) сохранить предупреждение перед ударом (тучи), чтобы противник успевал увести технику; 2) облик башни и молний сделать своим, галерею вики не копировать.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A tall storm-generation tower, a superweapon emitter that projects a lightning storm over a chosen area. It stands on a concrete base plate matching a 3×3 footprint, the slender tower about four and a half units tall with an emitter crown at the top. Light brushed metal, off-white panels, concrete plinth and cool blue emitter glow in the crown. The emitter crown is a separate part. One flat panel in pure magenta #FF00FF on the front of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A tall storm-generation tower, a superweapon emitter that projects a lightning storm over a chosen area. It stands on a concrete base plate matching a 3×3 footprint, the slender tower about four and a half units tall with an emitter crown at the top. Light brushed metal, off-white panels, concrete plinth and cool blue emitter glow in the crown. The emitter crown is a separate part. One flat panel in pure magenta #FF00FF on the front of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A tall storm-generation tower, a superweapon emitter that projects a lightning storm over a chosen area. It stands on a concrete base plate matching a 3×3 footprint, the slender tower about four and a half units tall with an emitter crown at the top. Light brushed metal, off-white panels, concrete plinth and cool blue emitter glow in the crown. The emitter crown is a separate part. One flat panel in pure magenta #FF00FF on the front of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание (подвижных частей нет), излучатель как отдельный объект (если понадобится)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 3×3 — по порту OpenRA (рабочее значение); форма башни (appearance.md); цвет игрока; состояние «Damaged» — в галерее вики есть, описания нет (appearance.md).
- Расхождение (выбрать одно значение): задержка до удара — в README `LightningDeferment` 250 кадров (≈17 с при 15 кадрах/с); appearance.md по вики — ≈12 с до туч и ≈2 с до молний (≈14 с). Записать выбранное значение и источник (animations.md).
- Спорные места для прототипа: заметно ли предупреждение за 12–17 с до первого удара; какой из двух видов (пассивный или заряженный) соответствует готовности к удару; что будет с бурей, если установку уничтожили (README молчит).
