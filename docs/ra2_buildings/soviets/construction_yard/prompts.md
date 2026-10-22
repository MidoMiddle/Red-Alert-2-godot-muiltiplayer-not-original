# Стройплощадка — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 4×4 клеток · Роль: ядро базы
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Главное здание базы, из которого строятся все остальные постройки и оборона. В силуэте оно читается как тяжёлый квадратный комплекс с высокой стрелой крана и широкими воротами. В нашей версии это собственный грубый промышленный блок Союза, а не копия оригинала.

## Требования к этой модели

- **Размер:** 4×4 клеток = 4×4 единиц Godot (рабочее значение). В таблице README 4×4; заметки README и план M3 дают 3×3 для нашей версии.
- **Высота:** около двух танков в высоту; стрела крана выше (предложение).
- **Подвижные части (отдельные объекты):** краны (`spin_crane_*`).
- **Узлы в Blender:** `spin_crane_*` (краны), `damage_*` (дым и огонь при повреждении), `debris_*` (обломки при разрушении). Предложение, см. `animations.md`.
- **Цвет команды:** плоская панель над воротами; в референсе окрашена `#FF00FF`.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение). См. [гайд, раздел 4](../../generation_guide.md).
- **Особенности:** крупный квадратный силуэт с выраженным фасадом и воротами, по нему сразу виден главный объект базы (сохраняем); кран и отделка свои, галерейные рисунки не копируем (делаем своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A heavy square base hub building where other structures are built. Blocky compound about twice the height of a tank, with a tall crane arm on the roof. It stands on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. The crane arm is a separate part, and a wide gate sits on the front face. One flat panel in pure magenta #FF00FF above the gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A heavy square base hub building where other structures are built. Blocky compound about twice the height of a tank, with a tall crane arm on the roof. It stands on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. The crane arm is a separate part, and a wide gate sits on the front face. One flat panel in pure magenta #FF00FF above the gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A heavy square base hub building where other structures are built. Blocky compound about twice the height of a tank, with a tall crane arm on the roof. It stands on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. The crane arm is a separate part, and a wide gate sits on the front face. One flat panel in pure magenta #FF00FF above the gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, краны (`spin_crane_*`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему (из `appearance.md` и `animations.md`):
  - высота относительно юнитов: в README её нет, в `appearance.md` «не подтверждена»; значение выше — предложение;
  - движется ли кран в простое и что видно при работе: источники не описывают, галерея C&C Wiki в этой сессии недоступна (402/403);
  - вид при повреждении (до ~50% и ~25% HP) и при продаже не описан; анимация разрушения по C&C Wiki есть, но в игре не использовалась, обломки не описаны; клипов `open` и `close` нет, анимация ворот не описана (`appearance.md`, `animations.md`);
  - акценты, эмблемы и цвет игрока не подтверждены (`appearance.md`); в промпте их нет, цветная панель над воротами — наше решение.
- Спорные места, которые проверим в прототипе:
  - размер: в README 4×4, в плане M3 3×3; `appearance.md` предлагает проверить рядом с соседними зданиями 3×3 и 4×3;
  - крутить ли `active` всю стройку или один раз на здание, и стоит ли кран в простое; вариант `active` вместо `spin_crane_*` открыт (`animations.md`);
  - как параллельная стройка в двух очередях и смена цвета команды ложатся на кран и корпус (`animations.md`);
  - видно ли направление ворот при разных углах камеры: в финальной версии ворота смотрят на юго-восток, в ранней на юго-запад (`appearance.md`).
