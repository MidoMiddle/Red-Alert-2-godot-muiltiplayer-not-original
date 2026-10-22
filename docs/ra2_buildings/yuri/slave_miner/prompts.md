# Рабовладельческий рудник — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 4×3 клетки · Роль: экономика (перерабатывающий завод и сборщик)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Экономика Культа в одном объекте: тяжёлая бронированная платформа, которая в развёрнутом виде перерабатывает руду в кредиты. В силуэте читаются широкая приёмная воронка и рампы для разгрузки. Корпус и набор рабочих юнитов свои, не копия оригинала.

## Требования к этой модели

- **Размер:** 4×3 клетки = 4×3 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — перерабатывающий завод Советов, 4×3 (один ряд — разгрузочная площадка).
- **Высота:** около высоты танка (предложение). Промпт описывает развёрнутую форму (здание); машинная форма — отдельный вопрос.
- **Подвижные части (отдельные объекты):** одна часть и основание (в развёрнутой форме подвижных частей не подтверждено). Рабочие юниты — отдельные юниты, не части здания.
- **Узлы в Blender:** `open` и `close` для смены формы (предложение); `active`, пока рабочие юниты добывают (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стороне платформы (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Две формы одного объекта: переход «машина ↔ здание» должен читаться по силуэту. Сохраняем.
  2. Корпус и набор рабочих юнитов делаем свои, без копирования оригинального набора. Проверить, видны ли рабочие юниты с расстояния.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A heavy ore-processing plant that turns raw ore into credits, with a wide intake funnel on one side and loading ramps on the other. Wide, roughly tank height. On a flat rectangular base plate matching a 4×3 footprint, dark violet armoured organic-industrial plating with a cool glowing accent along the ramp edges. One flat panel in pure magenta #FF00FF on the side of the platform. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A heavy ore-processing plant that turns raw ore into credits, with a wide intake funnel on one side and loading ramps on the other. Wide, roughly tank height. On a flat rectangular base plate matching a 4×3 footprint, dark violet armoured organic-industrial plating with a cool glowing accent along the ramp edges. One flat panel in pure magenta #FF00FF on the side of the platform. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A heavy ore-processing plant that turns raw ore into credits, with a wide intake funnel on one side and loading ramps on the other. Wide, roughly tank height. On a flat rectangular base plate matching a 4×3 footprint, dark violet armoured organic-industrial plating with a cool glowing accent along the ramp edges. One flat panel in pure magenta #FF00FF on the side of the platform. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Какая форма генерируется: по гайду один объект здания. В промптах описана развёрнутая форма. Машинная форма (техника, `VehicleAnimator`), отдельная модель или клипы смены формы — открытый вопрос.
- Размер не подтверждён; рабочее значение 4×3 взято по перерабатывающему заводу Советов. Проверить в прототипе на поле.
- Не подтверждено (appearance.md, animations.md): форма корпуса, высота, цвет, вид развёртывания (галерея C&C Wiki, не сверена), наличие колёс или гусениц у машинной формы, вид рабочих юнитов.
- Проверим в прототипе: читается ли развёрнутость по силуэту и видны ли рабочие юниты с расстояния (сами юниты в этом промпте не описаны).
