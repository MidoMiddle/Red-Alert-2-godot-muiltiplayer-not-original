# Аэропорт — промпты для генерации

> Фракция: нейтральное · Размер: 3×3 клеток · Роль: поддержка (десант пехоты при захвате)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Гражданский терминал 3×3: зал с плоской крышей и площадкой перед входом. В силуэте читается транспортный узел, но функция десанта по виду не видна: её показывает самолёт, который в модель не входит и появляется с края карты как отдельный объект (animations.md). В нашей версии важнее, чтобы самолёт был виден на карте, а терминал был собственным, без копирования облика (appearance.md).

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot (рабочее значение: по README, по источникам не сверено). В README сказано, что на части театров форма другая: не подтверждено, в промптах базовые 3×3.
- **Высота:** зал примерно как танк (предложение). Высота относительно юнитов не подтверждена.
- **Подвижные части (отдельные объекты):** одна часть и основание. Самолёт десанта — отдельный объект, не часть этой модели (animations.md).
- **Узлы в Blender:** `damage_*` с дымом и огнём (соглашение проекта, виды — предложения); `light_*` на полосе во время сброса (предложение). Клипов нет.
- **Цвет команды:** одна плоская панель `#FF00FF` на фасаде над входом (предложение: цвет игрока в источниках не подтверждён).
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение).
- **Особенности:**
  - Сохранить видимый самолёт, который прилетает и сбрасывает пехоту: это обратная связь для способности (appearance.md). В этой модели его нет, поэтому проверяем его отдельно.
  - Сделать своим силуэт терминала и самолёта, без копирования конкретного вида (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A civilian air terminal building, a passenger hall, on a flat rectangular base plate matching a 3×3 footprint. The hall is about the height of a battle tank, with a flat roof and a low parapet. Dark concrete and warm brick walls with neutral grey trim, no faction accents. A painted apron strip lies across the base plate in front of the entrance. One flat panel in pure magenta #FF00FF on the front facade above the entrance. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A civilian air terminal building, a passenger hall, on a flat rectangular base plate matching a 3×3 footprint. The hall is about the height of a battle tank, with a flat roof and a low parapet. Dark concrete and warm brick walls with neutral grey trim, no faction accents. A painted apron strip lies across the base plate in front of the entrance. One flat panel in pure magenta #FF00FF on the front facade above the entrance. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, aircraft, background, ground outside the base, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A civilian air terminal building, a passenger hall, on a flat rectangular base plate matching a 3×3 footprint. The hall is about the height of a battle tank, with a flat roof and a low parapet. Dark concrete and warm brick walls with neutral grey trim, no faction accents. A painted apron strip lies across the base plate in front of the entrance. One flat panel in pure magenta #FF00FF on the front facade above the entrance. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание (самолёт не в этой модели)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: вид терминала, окон и антенн (appearance.md помечает как не подтверждённое); облик самолёта. README описывает самолёты у терминала как часть облика здания, animations.md относит самолёт к отдельному объекту. Из-за запрета «no vehicles» в промпт самолёт не включён.
- Спорные места, которые проверим в прототипе: виден ли самолёт у края карты и не теряется ли он на фоне; различим ли состав десанта по стороне владельца; нужен ли `light_*` на полосе во время сброса.
