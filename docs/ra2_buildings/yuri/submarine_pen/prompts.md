# Подводная верфь — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 4×4 клетки (на воде) · Роль: производство (флот)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Верфь Культа ставится только на воду. В силуэте должны читаться крытый док и слип, уходящий в море; форма верфи в источниках не описана, поэтому это предложение. Корпус и палитра свои.

## Требования к этой модели

- **Размер:** 4×4 клетки = 4×4 единицы Godot (рабочее значение). В README: «не подтверждено (ставится на воду)»; значение по аналогу — верфь Советов, 4×4 (только на воде); у Союзников 4×4 по ModEnc.
- **Высота:** ниже танка над водой (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (выпуск кораблей визуально не описан; шлюз — только если понадобится, предложение).
- **Узлы в Blender:** `damage_*` (соглашение); `open` — только если появится шлюз (предложение).
- **Цвет команды:** одна плоская панель `#FF00FF` на стене со стороны причала (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Размещение только на воде — главный признак верфи на карте, если оно сохранится. Сохраняем.
  2. Корпус и палитру делаем свои; флот минимальный (по README), форму верфи не копируем.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A covered dock built on water, with a sloped slip leading from it into the sea. Shorter than a tank above the water. On a flat rectangular base plate matching a 4×4 footprint at water level, a low dark violet organic-industrial structure with a cool glowing accent along the dock edge. Water only inside the footprint, no water or ground beyond it. One flat panel in pure magenta #FF00FF on the side wall facing the quay. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A covered dock built on water, with a sloped slip leading from it into the sea. Shorter than a tank above the water. On a flat rectangular base plate matching a 4×4 footprint at water level, a low dark violet organic-industrial structure with a cool glowing accent along the dock edge. Water only inside the footprint, no water or ground beyond it. One flat panel in pure magenta #FF00FF on the side wall facing the quay. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra water surface, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A covered dock built on water, with a sloped slip leading from it into the sea. Shorter than a tank above the water. On a flat rectangular base plate matching a 4×4 footprint at water level, a low dark violet organic-industrial structure with a cool glowing accent along the dock edge. Water only inside the footprint, no water or ground beyond it. One flat panel in pure magenta #FF00FF on the side wall facing the quay. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единицы после нормализации
- [ ] Pivot на уровне воды, перёд −Z (см. примечания: гайд говорит про землю)
- [ ] Части разделены: корпус с основанием (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Здание откладывается.** Флот вне MVP (README и `04_gameplay.md`). Промпт готов заранее; в очередь пайплайна не ставим.
- **Pivot.** Гайд требует центр футпринта на земле. Для здания на воде предлагаем уровень воды. Решить до пайплайна.
- Размер не подтверждён; рабочее значение 4×4 по советскому и союзному аналогам (оба — 4×4).
- Не подтверждено: причалы, слип и выход кораблей, вид в покое, поведение без энергии, визуал при повреждении, разрушении и продаже. Анимация строительства — по галерее C&C Wiki, не сверена.
- Проверим в прототипе: видна ли верфь с суши и с моря и читается ли точка выхода кораблей.
