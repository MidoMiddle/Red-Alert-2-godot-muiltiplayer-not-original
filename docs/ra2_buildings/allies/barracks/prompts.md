# Казармы — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 2×3 клетки · Роль: производство (пехота)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Казармы обучают пехоту Альянса и выпускают её через дверь. В силуэте читаются невысокий металлический корпус и смотровая вышка с флагом: по ним здание отличается от складов и заводов. В нашей версии облик свой: светлый металл, синие акценты и флаг на вышке как опознавательный знак.

## Требования к этой модели

- **Размер:** 2×3 клетки = 2×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено; appearance.md — не подтверждено источниками).
- **Высота:** корпус около 1.5 единицы, вышка около 2.5 (предложение).
- **Подвижные части (отдельные объекты):** дверь выхода пехоты `door` (предложение; по README дверь есть, на картинке не сверено). Флаг `flag` (предложение, необязательно).
- **Узлы в Blender:** `door` с клипами `open` и `close` (предложение); `flag` (предложение, необязательно); `damage_*` — дым и искры (предложение). Клипа `active` нет.
- **Цвет команды:** плоский флаг на вышке — зона `#FF00FF` (предложение: флаг подтверждён галереей вики, его цвет и цвет игрока — нет).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — металл, сборная конструкция (вики).
- **Особенности:** 1) сохранить опознавательный знак — вышку или флаг, чтобы казармы не путали с другими постройками; 2) флаг и цветовые акценты сделать своими.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
An infantry training hall that trains foot soldiers, a sturdy metal building with a watchtower on one end. The hall is about 1.5 units tall and the watchtower about 2.5, on a flat rectangular base plate matching a 2×3 footprint. Light brushed metal panels, off-white trim, tinted glass and cool blue accent lights. A single exit door on the front wall is a separate part. One flat flag panel in pure magenta #FF00FF hangs from the watchtower. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
An infantry training hall that trains foot soldiers, a sturdy metal building with a watchtower on one end. The hall is about 1.5 units tall and the watchtower about 2.5, on a flat rectangular base plate matching a 2×3 footprint. Light brushed metal panels, off-white trim, tinted glass and cool blue accent lights. A single exit door on the front wall is a separate part. One flat flag panel in pure magenta #FF00FF hangs from the watchtower. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
An infantry training hall that trains foot soldiers, a sturdy metal building with a watchtower on one end. The hall is about 1.5 units tall and the watchtower about 2.5, on a flat rectangular base plate matching a 2×3 footprint. Light brushed metal panels, off-white trim, tinted glass and cool blue accent lights. A single exit door on the front wall is a separate part. One flat flag panel in pure magenta #FF00FF hangs from the watchtower. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, дверь (`door`), флаг (`flag`, если оставляем)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 2×3 (README; appearance.md — не подтверждено источниками); учебный центр и жилые помещения снаружи не описаны; дверь на картинке не сверена; цвет игрока не подтверждён.
- Спорные места для прототипа: флаг как зона команды — наше решение (в альфа-версии флаг стоял в другом месте, appearance.md); нужны ли колыхание флага и дверь с клипами `open`/`close` — предложения из animations.md.
