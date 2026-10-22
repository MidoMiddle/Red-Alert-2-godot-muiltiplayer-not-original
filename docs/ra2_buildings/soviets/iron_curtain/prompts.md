# Железный занавес — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 3×3 клеток · Роль: супероружие (поддержка)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Массивное основание 3×3 с большой металлической сферой и дугами энергии: по силуэту читается защитное поле. В нашей версии сфера, дуги и поле на целях свои; поле — шейдер, а не часть здания.

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot.
- **Высота:** около двух с половиной высот танка вместе со сферой (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` движущихся частей нет).
- **Узлы в Blender:** `light_arc_*` (два уровня: пассивный и заряженный; предложение), клип `active` (заряженный вид; предложение), `damage_*` (дым; огонь при сильном повреждении). `spin_*`, `smoke_*`, клипы `open` и `close` не нужны.
- **Цвет команды:** панель `#FF00FF` на боковой стороне основания.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение) — по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сфера на широкой базе и дуги по силуэту читаются как защитное поле; форму дуг и цвет поля делаем своими, не чёрно-красными.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A massive shield emitter building, a wide heavy base carrying a large metal sphere on top, about two and a half tank heights tall. Energy arcs circle the sphere and link it to the base. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. Armored plates cover the base; the arcs glow muted red. One flat panel in pure magenta #FF00FF on the side of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A massive shield emitter building, a wide heavy base carrying a large metal sphere on top, about two and a half tank heights tall. Energy arcs circle the sphere and link it to the base. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. Armored plates cover the base; the arcs glow muted red. One flat panel in pure magenta #FF00FF on the side of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A massive shield emitter building, a wide heavy base carrying a large metal sphere on top, about two and a half tank heights tall. Energy arcs circle the sphere and link it to the base. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. Armored plates cover the base; the arcs glow muted red. One flat panel in pure magenta #FF00FF on the side of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Высота не подтверждена (`appearance.md`); значение — предложение. Цвет игрока не подтверждён; зону `#FF00FF` выбрали нами.
  - Положение дуг расходится: в `README.md` дуги «вокруг сферы», в `appearance.md` «между базой и сферой». Промпт описывает оба варианта (см. спорные места).
  - Красный цвет дуг: `appearance.md` ссылается на `README.md`, но в README про цвет дуг ничего нет (красный оттенок относится к полю на целях). Цвет дуг — акцент палитры Союза (предложение).
  - Пассивный и заряженный виды: отличие не описано, когда включается заряженный, не подтверждено (`appearance.md`, `animations.md`).
  - Вращение сферы и движение дуг не описаны (`animations.md`).
  - Buildup, повреждённое состояние, разрушение и продажа: вид не описан (`appearance.md`, `animations.md`). C&C Wiki в этой сессии недоступна (402/403).
  - Гибель пехоты в зоне визуально не описана (`appearance.md`). Поле на целях — шейдер, не часть модели (`animations.md`).
- Спорные места, которые проверим в прототипе:
  - Положение дуг: вокруг сферы или между базой и сферой. Решить по силуэту на прототипе.
  - Чем заряженный вид отличается от пассивного и что показывать на паузе таймера (нехватка энергии, сброс шпионом; `README.md`, в игре не сверено).
  - Читаются ли зона 3×3 и опасность для пехоты без интерфейса (`appearance.md`).
