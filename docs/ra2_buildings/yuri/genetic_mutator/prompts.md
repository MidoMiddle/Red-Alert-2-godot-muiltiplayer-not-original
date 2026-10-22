# Генетический мутатор — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 3×3 клетки · Роль: супероружие (поддержка)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Супероружие поддержки Культа: при активации пехота в выбранной области превращается в бойцов Культа. В силуэте читаются два состояния: неактивный излучатель и светящееся кольцо в заряде. Форма здания и анимация активации свои; мотив мутации передаём светом и частицами, без копирования оригинала.

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — супероружие Советов: Железный занавес, 3×3.
- **Высота:** выше танка (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (активация — клип `active`; отдельных движущихся частей не описано).
- **Узлы в Blender:** `active` на активацию (предложение); `light_*` для состояния «заряжено» (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на передней грани (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Явный признак «заряжено» сохраняем: игрок видит готовность супероружия без чтения таймера.
  2. Форму здания и анимацию активации делаем свои; мотив мутации передаём иначе, чем в оригинале.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact superweapon emitter with a large ring-shaped emitter on its roof, shown in its inactive state with the ring dark. Taller than a tank. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial housing with a cool glowing accent. One flat panel in pure magenta #FF00FF on the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact superweapon emitter with a large ring-shaped emitter on its roof, shown in its inactive state with the ring dark. Taller than a tank. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial housing with a cool glowing accent. One flat panel in pure magenta #FF00FF on the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact superweapon emitter with a large ring-shaped emitter on its roof, shown in its inactive state with the ring dark. Taller than a tank. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial housing with a cool glowing accent. One flat panel in pure magenta #FF00FF on the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием и излучателем (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. Рабочее значение 3×3 по советскому аналогу (Железный занавес, 3×3).
- Не подтверждено: форма и высота, вид неактивного и заряженного состояний (в галерее C&C Wiki есть изображения, описания нет), вид активации, визуал превращения пехоты, визуал без энергии, при повреждении, разрушении и продаже.
- Референс — неактивное состояние, кольцо тёмное. Свечение заряда делаем через `light_*` (предложение).
- Проверим в прототипе: читается ли заряд с расстояния и видна ли зона эффекта на пехоте.
