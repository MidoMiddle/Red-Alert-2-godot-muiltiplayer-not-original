# Секретная лаборатория — промпты для генерации

> Фракция: нейтральное · Размер: 3×3 клеток · Роль: технологии (открывает один объект из пула)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Закрытый научный комплекс 3×3 с глухими стенами и одним входом. Это один объект из пула: модель описывает одну лабораторию, а не то, что она откроет. Какой объект откроется, выбирается при загрузке карты из восьми вариантов (пул YR, README), поэтому внешне это не видно; после захвата в сайдбаре появляется иконка (appearance.md).

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot (рабочее значение: по README; по источникам не сверено, см. appearance.md).
- **Высота:** около высоты танка, с верхним ярусом (предложение). Высота относительно юнитов не подтверждена.
- **Подвижные части (отдельные объекты):** одна часть и основание (клипов нет, animations.md).
- **Узлы в Blender:** `damage_*` с дымом (до ~50 % HP) и огнём (до ~25 %) — предложения. `light_*`, который горит, пока объект открыт, — предложение из animations.md; в оригинале индикатора нет (README), поэтому его можно отложить.
- **Цвет команды:** одна плоская панель `#FF00FF` на фасаде у входа (предложение: цвет игрока в источниках не подтверждён).
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение). Броня «сталь» из README — игровое свойство, во внешний вид не переносим.
- **Особенности:**
  - Сохранить отличимый маркер техздания, чтобы лаборатория читалась на карте как технологическое здание (appearance.md).
  - Сделать своим индикатор «что даст лаборатория»: в оригинале результат скрыт до захвата, у нас можно показывать категорию или «неизвестно» (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A closed research laboratory building, a compact science complex with sealed walls, on a flat rectangular base plate matching a 3×3 footprint. The main block is about the height of a battle tank, with a flat roof and a stepped upper floor. Concrete walls with brick trim, narrow slit windows and neutral grey tones, no faction accents. One flat panel in pure magenta #FF00FF on the front wall beside a sealed entrance door. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A closed research laboratory building, a compact science complex with sealed walls, on a flat rectangular base plate matching a 3×3 footprint. The main block is about the height of a battle tank, with a flat roof and a stepped upper floor. Concrete walls with brick trim, narrow slit windows and neutral grey tones, no faction accents. One flat panel in pure magenta #FF00FF on the front wall beside a sealed entrance door. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A closed research laboratory building, a compact science complex with sealed walls, on a flat rectangular base plate matching a 3×3 footprint. The main block is about the height of a battle tank, with a flat roof and a stepped upper floor. Concrete walls with brick trim, narrow slit windows and neutral grey tones, no faction accents. One flat panel in pure magenta #FF00FF on the front wall beside a sealed entrance door. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
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

- Что не подтверждено и почему: облик лаборатории (README описывает его, в игре не сверено); вид на здании при открытии объекта (визуала нет, animations.md); цвет игрока; размер 3×3 по источникам не сверен. Выдача поиска «Yuri battle lab» в C&C Wiki: animations.md считает её другим зданием, appearance.md предлагает проверить, не путаем ли мы их.
- Спорные места, которые проверим в прототипе: читаемость иконки в сайдбаре после захвата; нужен ли сигнал выбора объекта при захвате (в оригинале, по README, его нет).
