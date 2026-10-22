# Командование ВВС — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 3×2 клетки · Роль: радар и аэродром, тех-уровень 2
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Командование ВВС совмещает радар и аэродром с четырьмя посадочными площадками: невысокое здание с антенной радара. В силуэте читаются антенна и площадки для самолётов. В нашей версии облик свой: светлый металл, стекло и синие огни; связку «радар + аэродром» в одном здании сохраняем.

## Требования к этой модели

- **Размер:** 3×2 клетки = 3×2 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** корпус около 1.5 единицы, антенна радара до 3 (предложение).
- **Подвижные части (отдельные объекты):** антенна радара `spin_radar` (предложение; вращение по README не описано). Четыре пустые метки посадки `dock_1`–`dock_4` (предложение; сами самолёты — вне модели здания).
- **Узлы в Blender:** `spin_radar` (предложение), `light_*` — огни (предложение), пустые узлы `dock_1`–`dock_4` (предложение), `damage_*` (предложение), `debris_*` (предложение). Клипов `open`, `close`, `active` нет.
- **Цвет команды:** плоская панель `#FF00FF` на фасаде под антенной (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — не подтверждена.
- **Особенности:** 1) сохранить связку «радар + аэродром» в одном здании, если авиация войдёт в версию; 2) облик площадок и радара сделать своим, можно добавить светлую отделку и синие огни.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
An aviation control building combining a radar station and an aircraft landing area, a low structure with a radar antenna. It stands on a flat rectangular base plate matching a 3×2 footprint, about one and a half units tall. Light brushed metal, off-white trim, tinted glass and cool blue accent lights. The radar antenna is a separate part and four landing pads are marked on the apron. One panel in pure magenta #FF00FF on the front face below the antenna. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
An aviation control building combining a radar station and an aircraft landing area, a low structure with a radar antenna. It stands on a flat rectangular base plate matching a 3×2 footprint, about one and a half units tall. Light brushed metal, off-white trim, tinted glass and cool blue accent lights. The radar antenna is a separate part and four landing pads are marked on the apron. One panel in pure magenta #FF00FF on the front face below the antenna. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
An aviation control building combining a radar station and an aircraft landing area, a low structure with a radar antenna. It stands on a flat rectangular base plate matching a 3×2 footprint, about one and a half units tall. Light brushed metal, off-white trim, tinted glass and cool blue accent lights. The radar antenna is a separate part and four landing pads are marked on the apron. One panel in pure magenta #FF00FF on the front face below the antenna. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, антенна радара (`spin_radar`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 3×2 — по порту OpenRA (рабочее значение); вращение радара и его скорость не описаны (appearance.md, animations.md); покой не подтверждён; ключ `UnderRoofDoorAnim` у этого здания не подтверждён (animations.md); цвет игрока не подтверждён.
- Спорные места для прототипа: виден ли радар вращающимся при питании; читается ли по `build_up`, что здание готово, а не строится; нужны ли метки площадок, пока авиации нет в MVP.
