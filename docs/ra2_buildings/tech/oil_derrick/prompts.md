# Нефтяная вышка — промпты для генерации

> Фракция: нейтральное · Размер: 2×2 клеток · Роль: экономика (доход при захвате)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Нефтяная буровая установка 2×2 с качающимся насосом-балансиром: в силуэте читаются мачта-«качалка» и корпус на бетонном основании. На карте это нейтральная точка, которую захватывают инженером ради дохода. В нашей версии знак точки свой: жёлтый флаг с гаечным ключом не копируем (appearance.md, раздел «Для нашей версии»).

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot (рабочее значение: по README, по источникам не сверено, см. appearance.md).
- **Высота:** около полутора высот танка, насос заметно выше пехоты (предложение). Высота относительно юнитов в источниках не подтверждена.
- **Подвижные части (отдельные объекты):** насос-балансир `pump_arm`. Качается, а не вращается, поэтому не `spin_*`. Корпус и основание — одна часть.
- **Узлы в Blender:** клип `active` на `pump_arm` (зацикленное качание, пока вышка приносит доход); `damage_*` с дымом (до ~50 % HP) и огнём (до ~25 % HP). Узлы повреждений и `pump_arm` — предложения из animations.md.
- **Цвет команды:** одна плоская панель `#FF00FF` на боковине кожуха у основания (предложение: цвет игрока в источниках не подтверждён; вышка переходит к владельцу захватом).
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение). Насос — тёмная сталь: по README это металлическая конструкция.
- **Особенности:**
  - Сохранить качание насоса как знак дохода (appearance.md).
  - Сделать своим форму и знак: жёлтый флаг с гаечным ключом не копировать (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A small oil pumpjack rig on a flat rectangular base plate matching a 2×2 footprint. Its frame is about one and a half times the height of a battle tank. Weathered concrete foundation and brick-clad housing with a dark steel frame. A rocking-beam pump arm on top is a separate part, pivoting on a central pin. One flat panel in pure magenta #FF00FF on the housing side near the base. Neutral civilian industrial look, no faction markings. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A small oil pumpjack rig on a flat rectangular base plate matching a 2×2 footprint. Its frame is about one and a half times the height of a battle tank. Weathered concrete foundation and brick-clad housing with a dark steel frame. A rocking-beam pump arm on top is a separate part, pivoting on a central pin. One flat panel in pure magenta #FF00FF on the housing side near the base. Neutral civilian industrial look, no faction markings. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A small oil pumpjack rig on a flat rectangular base plate matching a 2×2 footprint. Its frame is about one and a half times the height of a battle tank. Weathered concrete foundation and brick-clad housing with a dark steel frame. A rocking-beam pump arm on top is a separate part, pivoting on a central pin. One flat panel in pure magenta #FF00FF on the housing side near the base. Neutral civilian industrial look, no faction markings. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием, `pump_arm`
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: высота относительно танка и радарной башни; цвет игрока (ModEnc говорит про remap, для этой вышки не проверено); вид повреждений и руин; качается ли насос постоянно или только при доходе (appearance.md, animations.md). Две цветные части копии из мода (красная и синяя, форум PPM) не подтверждены по роли, на них не ориентируемся.
- Спорные места, которые проверим в прототипе: читаемость качания на карте и при отдалении; металл насоса против палитры гайда (бетон, кирпич); имя `pump_arm` — предложение, в списке узлов гайда его нет. В день генерации проверить, есть ли у Meshy поле негативного промпта для image-to-3D.
