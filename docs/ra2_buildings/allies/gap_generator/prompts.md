# Генератор помех — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 1×1 клетка (рабочее значение) · Роль: информация (маскировка базы)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Генератор помех — тонкая башня-излучатель, которая прячет от врагов зону вокруг себя радиусом 10 клеток. Сама башня мала: в силуэте читаются тонкий излучатель и огни. Завеса — эффект поля, не часть модели. В нашей версии облик свой; граница зоны — отдельный эффект на карте.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение: размер по умолчанию, не подтверждено).
- **Высота:** тонкая башня около 2.5 единицы (предложение).
- **Подвижные части (отдельные объекты):** нет (одна часть и основание).
- **Узлы в Blender:** `light_*` — огни на башне, горят при питании (предложение); `damage_*` (предложение); `debris_*` (предложение). Клипов нет. Зона завесы — отдельный эффект на поле, не узел.
- **Цвет команды:** полоса `#FF00FF` на корпусе башни над основанием (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить видимый радиус завесы (круг или затемнение), чтобы игрок понимал, что там не видно; 2) облик башни сделать своим: оригинал визуально не описан.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A slender jamming emitter tower used to conceal the area around a base, with small indicator lights near its top. It stands on a flat rectangular base plate matching a 1×1 footprint, the tower about two and a half units tall. Light brushed metal, off-white panels, tinted glass and cool blue indicator lights. The indicator lights are separate parts. One flat strip in pure magenta #FF00FF on the tower body above the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A slender jamming emitter tower used to conceal the area around a base, with small indicator lights near its top. It stands on a flat rectangular base plate matching a 1×1 footprint, the tower about two and a half units tall. Light brushed metal, off-white panels, tinted glass and cool blue indicator lights. The indicator lights are separate parts. One flat strip in pure magenta #FF00FF on the tower body above the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A slender jamming emitter tower used to conceal the area around a base, with small indicator lights near its top. It stands on a flat rectangular base plate matching a 1×1 footprint, the tower about two and a half units tall. Light brushed metal, off-white panels, tinted glass and cool blue indicator lights. The indicator lights are separate parts. One flat strip in pure magenta #FF00FF on the tower body above the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание (подвижных частей нет), огни (`light_*`) как отдельный объект
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 1×1 — размер по умолчанию, не подтверждён (README); высота и облик башни (appearance.md: вики форму не описывает, тонкая башня — по README); строительной анимации в галерее вики нет, поэтому `build_up` — наше предложение (appearance.md, animations.md); продажа в README не описана, ModEnc связывает продажу с Buildup.
- Спорные места для прототипа: читается ли граница зоны без подписи и не путается ли игрок с обычным туманом; видна ли башня издалека.
