# Зенитное орудие — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 1×1 клетка (рабочее значение) · Роль: оборона (ПВО)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Зенитная установка против воздушных целей: низкий бронированный корпус с крупным стволом на поворотном лафете, направленным в небо. В силуэте читаются большой ствол и наклон вверх, а не пехотная огневая точка. В нашей версии это дорогая ПВО на базе 1×1, которая стреляет только по воздуху и без энергии молчит.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение). В таблице README 1×1; заметки README и план M3 дают 2×2 для нашего MVP.
- **Высота:** корпус ниже танка, ствол поднят к небу, общая высота около двух ростов пехотинца (предложение). В README высоты нет; в `appearance.md` высота «не подтверждена».
- **Подвижные части (отдельные объекты):** `spin_turret` (поворотный лафет, pivot на оси вращения), `barrel` (ствол, откат) — из `animations.md` (предложение).
- **Узлы в Blender:** `muzzle_0` (пустой узел у среза ствола для вспышки и дыма), `damage_*` (дым с лафета, предложение; соглашение M1), `debris_*` (обломки при разрушении, предложение из `animations.md`).
- **Цвет команды:** в референсе окрашена `#FF00FF` боковая панель основания (наш выбор; в `appearance.md` цвет игрока не подтверждён).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** крупный ствол как признак ПВО, читается «против воздуха» (сохранить); форма ствола и лафета своя, силуэт из галереи вики не копируем (сделать своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact anti-aircraft emplacement that fires only at air targets. A low armoured base carries one large barrel on a rotating mount, raised to about twice a soldier's height. It stands on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. The rotating mount and the recoiling barrel are separate parts. One flat panel in pure magenta #FF00FF on the side plate of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact anti-aircraft emplacement that fires only at air targets. A low armoured base carries one large barrel on a rotating mount, raised to about twice a soldier's height. It stands on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. The rotating mount and the recoiling barrel are separate parts. One flat panel in pure magenta #FF00FF on the side plate of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact anti-aircraft emplacement that fires only at air targets. A low armoured base carries one large barrel on a rotating mount, raised to about twice a soldier's height. It stands on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. The rotating mount and the recoiling barrel are separate parts. One flat panel in pure magenta #FF00FF on the side plate of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: `spin_turret`, `barrel`
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Не подтверждено:**
  - размер для нашей версии: в таблице README и в `appearance.md` 1×1 (значение оригинала), в заметках README, в `animations.md` и в плане M3 2×2 (наш MVP); в промптах 1×1 (рабочее значение, по таблице README), перед генерацией подтвердить;
  - высота относительно юнитов (`appearance.md`: не подтверждена); в промпте значение предложенное;
  - материал основы, цвет игрока, вид в покое и при работе (`appearance.md`, `animations.md`: не подтверждено); вспышка, откат и скорость поворота не описаны;
  - вид без энергии, при повреждении и при разрушении не подтверждён (`animations.md`); порог 25% для сильного повреждения проектный.
- **Проверим в прототипе:**
  - хватает ли поворота лафета без подъёма ствола при быстрой смене воздушных целей;
  - видно ли, что орудие бьёт только по воздуху, и отличимо ли оно от пулемётной турели (в `appearance.md` проверка на сетке 1×1; если возьмём 2×2 из плана M3, проверить заново);
  - понятно ли, что без энергии орудие отключено.
