# Стройплощадка — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 4×4 клетки · Роль: ядро
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Ядро базы Культа: здесь строятся все остальные здания и оборона. В силуэте читается монтажная клешня, которая во время работы висит в воздухе над стройкой и показывает, что здесь идёт строительство. Корпус и палитра свои, без копирования оригинала.

## Требования к этой модели

- **Размер:** 4×4 клетки = 4×4 единицы Godot (рабочее значение). В README размер стоит «4×4 (не подтверждено)».
- **Высота:** выше танка, клешня висит над корпусом (предложение).
- **Подвижные части (отдельные объекты):** клешня (в `animations.md` назван `claw_*`, предложение). Корпус с основанием — одна часть.
- **Узлы в Blender:** клип `active` для клешни, пока идёт очередь (предложение); `damage_*` (соглашение). Имя `claw_*` в гайде не предусмотрено, см. примечания.
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стене корпуса (предложение: цвет игрока в источниках не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Клешню в работе сохраняем как признак строительства, а не как стиль.
  2. Корпус и палитру делаем своими. Зелёный оттенок из README в источниках не подтверждён, в промпт его не закладываем.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact construction hub that builds every other structure of a base, with a large assembly claw hovering in the air above its roof. Angular silhouette, taller than a tank. On a flat rectangular base plate matching a 4×4 footprint, a dark violet organic-industrial body with a cool glowing accent along its edges. The assembly claw is a separate part, suspended on an arm. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact construction hub that builds every other structure of a base, with a large assembly claw hovering in the air above its roof. Angular silhouette, taller than a tank. On a flat rectangular base plate matching a 4×4 footprint, a dark violet organic-industrial body with a cool glowing accent along its edges. The assembly claw is a separate part, suspended on an arm. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact construction hub that builds every other structure of a base, with a large assembly claw hovering in the air above its roof. Angular silhouette, taller than a tank. On a flat rectangular base plate matching a 4×4 footprint, a dark violet organic-industrial body with a cool glowing accent along its edges. The assembly claw is a separate part, suspended on an arm. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: клешня (`claw`), корпус с основанием
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено (appearance.md, animations.md): форма корпуса, высота, вид в покое, при повреждении и разрушении, продажа. Видимая деталь — только клешня в работе (C&C Wiki).
- Размер 4×4 не подтверждён в README; используем как рабочее значение.
- Палитра и место панели `#FF00FF` — предложение; цвет игрока не утверждён.
- `claw_*` из `animations.md` не входит в список узлов гайда (`spin_*`, `smoke_*`, `light_*`, `damage_*`). Решить: оставить клешню отдельным объектом без спец-узла или добавить узел в гайд.
- Проверим в прототипе: читается ли клешня на минимальном масштабе карты и не теряется ли она среди соседних зданий.
