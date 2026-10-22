# Дот — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 1×1 клетка · Роль: оборона (против пехоты)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Дот — дешёвый бронированный пулемётный бункер с мешками с песком; он работает без энергии и продолжает стрелять при блэкауте. В силуэте читаются низкий бункер и пулемётная турель. В нашей версии облик свой: сталь, светлая отделка и синий акцент — наше решение.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot.
- **Высота:** около 1 единицы, примерно в рост человека (предложение).
- **Подвижные части (отдельные объекты):** пулемётная турель `spin_turret` (предложение; поворот по appearance.md не описан).
- **Узлы в Blender:** `spin_turret` (предложение), `muzzle_*` — вспышка у ствола (предложение; имя вне соглашения гайда), `damage_*` — дым и огонь у амбразуры (предложение), `debris_*` (предложение). Клипов `open` и `close` нет.
- **Цвет команды:** полоса `#FF00FF` над амбразурой на лицевой стороне (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — сталь (вики, подтверждено); мешки с песком — подтверждено.
- **Особенности:** 1) сохранить бронированный бункер с пулемётом, чтобы оборона читалась как противопехотная; 2) материалы и отделку сделать своими; светлый бетон или сталь с синими акцентами — стиль фракции.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A small armoured machine-gun bunker that defends against infantry, a squat steel block with a turret on top and sandbags around its base. It stands on a flat rectangular base plate matching a 1×1 footprint, the bunker about one unit tall. Light brushed steel, sand-coloured sandbags and cool blue accent lights. The machine-gun turret and its barrel are separate parts. One flat strip in pure magenta #FF00FF on the front face above the firing slit. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A small armoured machine-gun bunker that defends against infantry, a squat steel block with a turret on top and sandbags around its base. It stands on a flat rectangular base plate matching a 1×1 footprint, the bunker about one unit tall. Light brushed steel, sand-coloured sandbags and cool blue accent lights. The machine-gun turret and its barrel are separate parts. One flat strip in pure magenta #FF00FF on the front face above the firing slit. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A small armoured machine-gun bunker that defends against infantry, a squat steel block with a turret on top and sandbags around its base. It stands on a flat rectangular base plate matching a 1×1 footprint, the bunker about one unit tall. Light brushed steel, sand-coloured sandbags and cool blue accent lights. The machine-gun turret and its barrel are separate parts. One flat strip in pure magenta #FF00FF on the front face above the firing slit. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание, турель с стволом (`spin_turret`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: цвет игрока; вид повреждения и разрушения; поворот турели и вспышка у ствола не описаны (appearance.md, animations.md); DamageFireOffsets — страница ModEnc не найдена.
- Расхождение (сверить): appearance.md (по вики) пишет, что внутри дота может стоять пехота, защищённая от пси-контроля; README о гарнизоне молчит и говорит только, что пехоты при разрушении нет. Если гарнизон есть, нужны вход и выход пехоты (не подтверждено; animations.md).
- Спорные места для прототипа: читается ли пулемёт и его поворот с 5,5 клетки; читается ли дот как противопехотный, если стоит у входа в базу.
