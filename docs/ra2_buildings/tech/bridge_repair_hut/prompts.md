# Хижина ремонта моста — промпты для генерации

> Фракция: нейтральное · Размер: 1×1 клетка · Роль: инфраструктура карты (ремонт мостов)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Маленькая будка 1×1 у конца моста: объект карты, а не постройка игрока. Нужна она для одного: инженер, войдя в хижину, чинит соседний мост. Мост в эту модель не входит, это отдельная задача карты; здесь описывается только хижина. По виду не видно, к какому мосту она привязана, это нужно показать на карте (appearance.md).

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение: по README; по источникам не сверено, см. appearance.md).
- **Высота:** ниже танка (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание. Дверь — отдельный объект, если нужен клип `open` (предложение).
- **Узлы в Blender:** `open` и `close` на двери — только если вход инженера нужно показать (предложение, animations.md). `damage_*` не нужен: хижина неуязвима (README).
- **Цвет команды:** не применяется: хижина не принадлежит игрокам (README; appearance.md). Зона `#FF00FF` не нужна, в промпте её нет.
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение).
- **Особенности:**
  - Сохранить видимую связь «хижина — мост», которую README предлагает задавать в данных карты; на экране её тоже нужно показать (appearance.md).
  - Сделать своим форму хижины и мостов (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A small roadside maintenance hut, a single-room concrete and brick booth with one doorway and a flat roof, on a flat square base plate matching a 1×1 footprint. It is lower than a battle tank. Plain neutral civilian look with concrete and brick, no faction accents. No team-colour panel: the hut belongs to no player. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles, no bridge, no road, no water.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A small roadside maintenance hut, a single-room concrete and brick booth with one doorway and a flat roof, on a flat square base plate matching a 1×1 footprint. It is lower than a battle tank. Plain neutral civilian look with concrete and brick, no faction accents. No team-colour panel: the hut belongs to no player. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles, no bridge, no road, no water.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, bridge, road, water, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A small roadside maintenance hut, a single-room concrete and brick booth with one doorway and a flat roof, on a flat square base plate matching a 1×1 footprint. It is lower than a battle tank. Plain neutral civilian look with concrete and brick, no faction accents. No team-colour panel: the hut belongs to no player. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles, no bridge, no road, no water.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание; дверь отдельно, если есть клип `open`
- [ ] Маска команды на зоне `#FF00FF` (не применяется: хижина не принадлежит игрокам)
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: вид входа и самой хижины (README и транскрипт, визуал не подтверждён); вид восстановления и обрушения моста (в источниках не найден, animations.md); высота относительно юнитов.
- Спорные места, которые проверим в прототипе: понятно ли игроку, что ремонт доступен любому игроку (хижина ничья, README); видна ли связь с разрушенным пролётом на большом масштабе карты.
