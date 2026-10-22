# Биореактор — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 2×2 клетки · Роль: энергия
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Электростанция Культа, которая даёт больше энергии, когда в ней находятся пехотинцы (в README — до пяти). В силуэте читаются компактная капсула с ядром и индикаторы у основания: пустой и заполненный вид должны отличаться, иначе игрок не поймёт, откуда берётся энергия. Корпус органический и свой, без копирования формы оригинала.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение). В README: «2×2 по README», но источники расходятся: статья электростанции даёт 2×3 для пустого реактора.
- **Высота:** около высоты танка (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` подвижных частей не описано; створка выхода — только если решим, предложение).
- **Узлы в Blender:** клип `active` для заполненного варианта (соглашение); `light_*` — индикатор числа жильцов от 0 до 5 (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковом кольце у основания (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Два визуальных состояния (пусто и заполнено) — главный сигнал, что энергия зависит от жильцов. Сохраняем.
  2. Органический или технический мотив делаем своим, без копирования формы реактора. Проверить, читается ли число жильцов с расстояния.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact bio-organic energy generator whose rounded capsule core sits in the middle of its body, with small indicator lights along the base. Roughly the height of a tank. On a flat rectangular base plate matching a 2×2 footprint, a dark violet organic shell with a cool glowing accent and one rounded entrance on the side. The capsule core is part of the body, with no moving parts. One flat panel in pure magenta #FF00FF on the side ring. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact bio-organic energy generator whose rounded capsule core sits in the middle of its body, with small indicator lights along the base. Roughly the height of a tank. On a flat rectangular base plate matching a 2×2 footprint, a dark violet organic shell with a cool glowing accent and one rounded entrance on the side. The capsule core is part of the body, with no moving parts. One flat panel in pure magenta #FF00FF on the side ring. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact bio-organic energy generator whose rounded capsule core sits in the middle of its body, with small indicator lights along the base. Roughly the height of a tank. On a flat rectangular base plate matching a 2×2 footprint, a dark violet organic shell with a cool glowing accent and one rounded entrance on the side. The capsule core is part of the body, with no moving parts. One flat panel in pure magenta #FF00FF on the side ring. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер спорный: README и `appearance.md` дают 2×2, статья электростанции в C&C Wiki — 2×3 для пустого реактора. Берём 2×2 как рабочее значение, проверить в прототипе.
- Не подтверждено: форма корпуса, цвет и свет, видимость пехоты внутри, вид выхода пехоты при опустошении, вид в покое, поведение без энергии, визуал при повреждении, разрушении и продаже.
- Два состояния (пусто и заполнено) в источниках описаны подписью галереи C&C Wiki; различий по цвету и свету не описано. Состояния делаем через `active` и `light_*` (предложение).
- Створка выхода (`open`/`close`) — предложение из `animations.md`; не подтверждено, что у здания есть створка.
- Проверим в прототипе: читается ли число жильцов (0–5) с расстояния и какой размер (2×2 или 2×3) лучше смотрится на карте.
