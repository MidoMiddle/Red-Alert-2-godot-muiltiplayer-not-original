# Аванпост — промпты для генерации

> Фракция: нейтральное · Размер: 4×3 клеток · Роль: оборона и ремонт техники
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Укреплённая ремонтная площадка 4×3 с краном у заезда и поворотной ракетной турелью. В силуэте читаются кран (ремонт) и турель (оборона). Вид турели не копируем: в источниках её облик описан по-разному (appearance.md), поэтому в промпте она общая, без конкретной установки.

## Требования к этой модели

- **Размер:** 4×3 клеток = 4×3 единиц Godot (рабочее значение: по README; по источникам не сверено). В README сказано «3 ряда непроходимы, остальное — площадка для заезда»: какие именно ряды, не ясно.
- **Высота:** корпус около высоты танка, кран и турель выше (предложение).
- **Подвижные части (отдельные объекты):** турель `turret` (поворот; в покое `spin_turret`, предложение из animations.md); кран `crane_arm` (клип `active` при ремонте, предложение). Корпус и основание — одна часть.
- **Узлы в Blender:** `spin_turret` (покой); клип `active` на `crane_arm`; `muzzle_0..1` для вспышек залпа (предложение из animations.md; в списке узлов гайда его нет); `damage_*` с дымом и огнём (предложения).
- **Цвет команды:** одна плоская панель `#FF00FF` на стене у заезда (предложение: цвет игрока в источниках не подтверждён).
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение). Турель и кран — тёмная сталь как деталь.
- **Особенности:**
  - Сохранить читаемую зону ремонта: кран и заезд, чтобы функция читалась без подписей (appearance.md).
  - Сделать своим турель и кран, не привязываясь к конкретной установке: источники расходятся (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A fortified field repair station on a flat rectangular base plate matching a 4×3 footprint. Low concrete walls and brick corner blocks, about the height of a battle tank, enclose a drive-in bay. A repair crane with a folding arm stands by the bay, its arm a separate part. A rotating missile turret on a round roof pivot is a separate part. One flat panel in pure magenta #FF00FF on the wall beside the bay. Neutral civilian-built look, no faction accents. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A fortified field repair station on a flat rectangular base plate matching a 4×3 footprint. Low concrete walls and brick corner blocks, about the height of a battle tank, enclose a drive-in bay. A repair crane with a folding arm stands by the bay, its arm a separate part. A rotating missile turret on a round roof pivot is a separate part. One flat panel in pure magenta #FF00FF on the wall beside the bay. Neutral civilian-built look, no faction accents. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A fortified field repair station on a flat rectangular base plate matching a 4×3 footprint. Low concrete walls and brick corner blocks, about the height of a battle tank, enclose a drive-in bay. A repair crane with a folding arm stands by the bay, its arm a separate part. A rotating missile turret on a round roof pivot is a separate part. One flat panel in pure magenta #FF00FF on the wall beside the bay. Neutral civilian-built look, no faction accents. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием, `turret`, `crane_arm`
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: стреляет ли нейтральный аванпост по игрокам (README предполагает, что нет); вид турели (в источниках «Patriot» или похожа на IFV); вид крана (в поиске не найден, animations.md); что значит «3 ряда непроходимы» при footprint 4×3.
- Спорные места, которые проверим в прототипе: читается ли площадка ремонта без подписей; нужна ли «вышка», которую README упоминает наряду с краном и турелью (в appearance.md и animations.md её нет); видно ли, что нейтральный аванпост не стреляет; как `muzzle_*` ложится на список узлов гайда.
