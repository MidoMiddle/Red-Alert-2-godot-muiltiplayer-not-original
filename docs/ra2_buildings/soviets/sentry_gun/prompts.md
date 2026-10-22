# Пулемётная турель — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 1×1 клетка · Роль: оборона (против пехоты)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Автоматическая турель против пехоты: низкий бронированный корпус с вращающейся башней и коротким стволом. В силуэте читаются малый размер и башня над корпусом, без высокой конструкции. В нашей версии это дешёвая огневая точка без энергии, которую ставят у входов в базу.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot.
- **Высота:** примерно в рост стоящего пехотинца, заметно ниже танка (предложение). В README высоты нет, в `appearance.md` высота «не подтверждена».
- **Подвижные части (отдельные объекты):** `turret` (башня, поворот), `barrel` (ствол, отдача) — из `animations.md` (предложение).
- **Узлы в Blender:** `damage_*` (1–2 узла на корпусе, предложение из `animations.md`), `muzzle_0` (пустой узел у ствола для вспышки и дыма).
- **Цвет команды:** в референсе окрашена `#FF00FF` боковая панель основания (наш выбор; в `appearance.md` цвет игрока не подтверждён).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** компактная огневая точка против пехоты, читается по форме и размеру 1×1 (сохранить); форма башни и укрытия своя, галерейный вид не копируем (сделать своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact automated defence turret against infantry: a squat armoured emplacement about the height of a standing soldier, on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. The rotating turret head sits on top with a short barrel; the turret head and the barrel are separate parts. One flat panel in pure magenta #FF00FF on the side plate of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact automated defence turret against infantry: a squat armoured emplacement about the height of a standing soldier, on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. The rotating turret head sits on top with a short barrel; the turret head and the barrel are separate parts. One flat panel in pure magenta #FF00FF on the side plate of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact automated defence turret against infantry: a squat armoured emplacement about the height of a standing soldier, on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. The rotating turret head sits on top with a short barrel; the turret head and the barrel are separate parts. One flat panel in pure magenta #FF00FF on the side plate of the base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: `turret`, `barrel`
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Не подтверждено:**
  - высота относительно юнитов (`appearance.md`: «не подтверждена»); в промпте высота предложенная;
  - материал основы, акценты и цвет игрока (`appearance.md`: не описаны); палитра — наше предложение;
  - анимация покоя, вспышка выстрела и значения art.ini для NALASR (`Buildup`, `TurretAnim`, `Anim` у `Vulcan`) — не подтверждены (`animations.md`);
  - вид повреждения и разрушения не подтверждён; порог 25% для сильного повреждения проектный (`animations.md`).
- **Проверим в прототипе:**
  - читаются ли поворот башни и вспышка на клетке 1×1 при выстреле раз в 26 кадров;
  - не пропадают ли поворот и стрельба при дефиците энергии (правило из README, предложение для M4);
  - не закрывают ли дым и огонь `damage_*` башню и ствол;
  - блокируют ли стены огонь турели (в README открыто, зависит от линии огня в M4).
