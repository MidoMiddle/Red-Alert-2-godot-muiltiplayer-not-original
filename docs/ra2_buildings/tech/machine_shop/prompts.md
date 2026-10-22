# Механическая мастерская — промпты для генерации

> Фракция: нейтральное · Размер: 3×3 клеток · Роль: поддержка (ремонт техники)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Промышленный цех 3×3: по README это гражданская мастерская, которая чинит технику владельца. В силуэте должны читаться широкий въезд и корпус цеха; ворота и краны в источниках не подтверждены, поэтому въезд в промпте — предложение. В нашей версии ремонт виден как знак над техникой (UI), а сам цех без анимации (animations.md).

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot (рабочее значение: по README; по источникам не сверено, см. appearance.md).
- **Высота:** одноэтажный корпус примерно как танк (предложение). Высота относительно юнитов не подтверждена.
- **Подвижные части (отдельные объекты):** одна часть и основание (здание без клипов, animations.md).
- **Узлы в Blender:** `damage_*` с дымом (до ~50 % HP) и огнём (до ~25 %) — предложения. Знак ремонта над техникой — UI, не узел здания. Клипов нет.
- **Цвет команды:** одна плоская панель `#FF00FF` над въездом (предложение: цвет игрока в источниках не подтверждён).
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение).
- **Особенности:**
  - Сохранить читаемый знак «ремонт идёт» над техникой: без него эффект не виден (appearance.md).
  - Сделать своим сам цех и знак: знак не обязан быть гаечным ключом (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A civilian repair workshop, a single-storey industrial building, on a flat rectangular base plate matching a 3×3 footprint. The hall is about the height of a battle tank, with a flat roof, a low side annex and a tall ventilation stack. Dark concrete and warm brick walls with a wide roll-up door on the front. One flat panel in pure magenta #FF00FF above the roll-up door. Neutral industrial look with no faction accents. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A civilian repair workshop, a single-storey industrial building, on a flat rectangular base plate matching a 3×3 footprint. The hall is about the height of a battle tank, with a flat roof, a low side annex and a tall ventilation stack. Dark concrete and warm brick walls with a wide roll-up door on the front. One flat panel in pure magenta #FF00FF above the roll-up door. Neutral industrial look with no faction accents. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A civilian repair workshop, a single-storey industrial building, on a flat rectangular base plate matching a 3×3 footprint. The hall is about the height of a battle tank, with a flat roof, a low side annex and a tall ventilation stack. Dark concrete and warm brick walls with a wide roll-up door on the front. One flat panel in pure magenta #FF00FF above the roll-up door. Neutral industrial look with no faction accents. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: вид здания (ворота, краны, трубы в appearance.md помечены как не подтверждённые); движение на самом здании при ремонте; цвет игрока; размер 3×3 по источникам не сверен. Промышленный облик взят из README и в игре не сверен.
- Спорные места, которые проверим в прототипе: как знак ремонта выглядит на большой группе танков и не перегружает ли экран; не путается ли он со знаком лечения госпиталя, если оба работают рядом. По README в MVP здание не нужно.
