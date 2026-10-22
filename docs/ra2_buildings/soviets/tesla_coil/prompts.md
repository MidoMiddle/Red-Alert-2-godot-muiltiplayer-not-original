# Тесла-катушка — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 1×1 клетка (рабочее значение) · Роль: оборона (основная противоназемная)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Высокая электрическая башня: узкий корпус с шаром-разрядником наверху и гнёздами для подключения солдат у верхней части основания. В силуэте читаются высота и светящийся шар, по ним видна электрическая оборона. В нашей версии это основная наземная оборона, которая тратит много энергии и молчит без питания, пока её не заряжают солдаты.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение, решение о футпринте не принято). В таблице README 1×1; заметки README и план M3 дают 2×2 для нашей версии.
- **Высота:** примерно в три роста пехотинца, выше танка (предложение). В README высоты нет; в `appearance.md` высота «не подтверждена».
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` вращения нет, в README `Turret=no`).
- **Узлы в Blender:** `light_sphere` (узел свечения шара-разрядника, мерцание emission), `light_socket_*` (узлы свечения у гнёзд подзарядки, по числу гнёзд; число не подтверждено), `bolt_origin` (пустой узел, откуда бьёт молния), `damage_*` (дым и огонь, предложение из `animations.md`).
- **Цвет команды:** в референсе окрашена `#FF00FF` боковая панель у основания, ниже гнёзд (наш выбор; в `appearance.md` цвет игрока не подтверждён).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** вертикальная башня с видимым разрядом читается как электрическая оборона (сохранить); форма шара и ствола своя, галерейные рисунки не копируем (сделать своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A tall high-voltage defence tower that fires instant lightning bolts at ground targets. A slender spire, about three standing soldiers tall, carries a glowing sphere on top and socket ports near its base. The tower stands on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the side of the base, below the socket ports. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A tall high-voltage defence tower that fires instant lightning bolts at ground targets. A slender spire, about three standing soldiers tall, carries a glowing sphere on top and socket ports near its base. The tower stands on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the side of the base, below the socket ports. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A tall high-voltage defence tower that fires instant lightning bolts at ground targets. A slender spire, about three standing soldiers tall, carries a glowing sphere on top and socket ports near its base. The tower stands on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the side of the base, below the socket ports. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Не подтверждено:**
  - футпринт: в таблице README 1×1 (значение оригинала), в заметках README, в плане M3 и в `animations.md` 2×2 для нашей версии; решения пока нет (`animations.md`). В промптах 1×1 (рабочее значение, по таблице README), перед генерацией решить;
  - высота относительно юнитов (`appearance.md`: не подтверждена); в промпте значение предложенное;
  - цвет и вид молнии у усиленного выстрела, число гнёзд подзарядки и вид при подзарядке (`appearance.md`, `animations.md`: не подтверждено); в узлах `light_socket_*` число не задано;
  - вид без энергии, при повреждении и при разрушении (`appearance.md`, `animations.md`: не подтверждено); материал основы и цвет игрока не описаны (`appearance.md`).
- **Проверим в прототипе:**
  - видна ли подзарядка от соседних солдат и различимы ли обычный и усиленный выстрелы (`appearance.md`);
  - читается ли «катушка молчит» без энергии, если вид в оригинале не подтверждён (`animations.md`);
  - правило подзарядки из README (пороги в 1, 2 и 3 заряжающих) проверим вместе с M4; в модели оно не отражается.
