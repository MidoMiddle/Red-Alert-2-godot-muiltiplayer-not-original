# Боевой бункер — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 2×2 клетки · Роль: оборона (гарнизонная)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Низкий бетонный дот 2×2 с бойницами: неподвижная огневая точка, которая стреляет только тогда, когда в неё посажена пехота. В силуэте читаются низкий массив и бойницы, без башни и подвижных частей. В нашей версии это дешёвая оборона без энергии, по виду которой видно, занят ли дот.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (README; `artmd.ini`: Foundation=2x2).
- **Высота:** около полутора ростов пехотинца, ниже танка (предложение). В README высоты нет; в `appearance.md` высота «не подтверждена».
- **Подвижные части (отдельные объекты):** одна часть и основание (`animations.md`: подвижных частей источники не описывают).
- **Узлы в Blender:** `garrison_*` (вид с гарнизоном, виден только у занятого дота; предложение из `animations.md`), `light_muzzle_0`…`light_muzzle_4` (вспышки у бойниц, по одной на место; предложение), `damage_*` (дым ниже 50% HP, огонь на красной полоске; соглашение M1), `debris_*` (обломки при разрушении; предложение из `animations.md`).
- **Цвет команды:** в референсе окрашена `#FF00FF` боковая стена дота между бойницами (наш выбор; в `appearance.md` цвет игрока не подтверждён).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** видимый гарнизон (пустой и занятый виды) — основа читаемости механики (сохранить); бойницы и облик дота свои, галерейные рисунки не копируем (сделать своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A squat reinforced concrete pillbox with firing slits in its walls, shown empty, about one and a half standing soldiers tall. The building is one solid part with no moving parts. It sits on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the side wall, between two firing slits. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A squat reinforced concrete pillbox with firing slits in its walls, shown empty, about one and a half standing soldiers tall. The building is one solid part with no moving parts. It sits on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the side wall, between two firing slits. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A squat reinforced concrete pillbox with firing slits in its walls, shown empty, about one and a half standing soldiers tall. The building is one solid part with no moving parts. It sits on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the side wall, between two firing slits. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Не подтверждено:**
  - высота относительно юнитов (`appearance.md`: не подтверждена); в промпте значение предложенное;
  - вид гарнизона: что видно у занятого дота, с какого числа жильцов включается занятый вид и как пехота садится и выходит (`appearance.md`, `animations.md`: не описано). Промпт описывает пустой дот, вид с гарнизоном — узел `garrison_*` (предложение);
  - вид повреждения: отдельный вид не подтверждён; порог 25% для сильного повреждения проектный, ModEnc в `ConditionRed` даёт 50% (`animations.md`);
  - материал основы: «бетон» взят из README, `appearance.md` просит проверить его перед закреплением; цвет игрока не подтверждён (`appearance.md`).
- **Проверим в прототипе:**
  - читаются ли пустой и занятый виды и число жильцов на дальнем плане (`appearance.md`);
  - совпадает ли очередь вспышек `light_muzzle_*` с выстрелами жильцов и видны ли бойницы (`animations.md`);
  - обновляется ли вид сразу при эвакуации, продаже и разрушении (`animations.md`).
