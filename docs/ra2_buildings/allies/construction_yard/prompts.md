# Стройплощадка — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 4×4 клетки · Роль: ядро (строит здания)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Стройплощадка — ядро базы: комплекс, который собирает каждое новое здание на месте установки. В силуэте читаются крупный корпус и краны над ним, это главный ориентир на базе. В нашей версии облик свой: светлый металл и холодный синий акцент по палитре Альянса, форма кранов наша.

## Требования к этой модели

- **Размер:** 4×4 клетки = 4×4 единицы Godot (рабочее значение: по порту OpenRA, art.ini не проверен).
- **Высота:** корпус около 2 единиц, стрелы кранов выше (предложение).
- **Подвижные части (отдельные объекты):** стрелы кранов (предложение: `spin_crane_*`; по animations.md движение кранов можно вести и клипом `active`). Корпус — отдельный объект.
- **Узлы в Blender:** `spin_crane_*` (предложение), клип `active` (предложение, работа кранов). Клипов `open` и `close` нет (не сверено).
- **Цвет команды:** одна плоская панель `#FF00FF` на фасаде корпуса между кранами (предложение: цвет игрока на здании не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — бетон по README, не сверено в игре.
- **Особенности:** 1) сохранить видимую стадию сборки (Buildup, `build_up`), чтобы «строится» и «готово» читались; 2) форму и ракурс кранов сделать своими, спрайт оригинала не повторять.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A large construction hub that assembles new buildings in place, its main structure about two units tall with lifting cranes on tall arms rising above it. It stands on a flat rectangular base plate matching a 4×4 footprint. Light brushed metal and off-white panels, tinted glass windows, cool blue accent lights. The crane arms are separate parts on rotating joints. One flat panel in pure magenta #FF00FF on the front face between the cranes. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A large construction hub that assembles new buildings in place, its main structure about two units tall with lifting cranes on tall arms rising above it. It stands on a flat rectangular base plate matching a 4×4 footprint. Light brushed metal and off-white panels, tinted glass windows, cool blue accent lights. The crane arms are separate parts on rotating joints. One flat panel in pure magenta #FF00FF on the front face between the cranes. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A large construction hub that assembles new buildings in place, its main structure about two units tall with lifting cranes on tall arms rising above it. It stands on a flat rectangular base plate matching a 4×4 footprint. Light brushed metal and off-white panels, tinted glass windows, cool blue accent lights. The crane arms are separate parts on rotating joints. One flat panel in pure magenta #FF00FF on the front face between the cranes. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, стрелы кранов
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 4×4 взят из порта OpenRA, art.ini не проверен (README); высота и силуэт не описаны (appearance.md); цвет игрока не подтверждён; основа — бетон по README, не сверено в игре.
- Расхождение (сверить в игре): appearance.md пишет, что движение в покое не подтверждено; animations.md по ModEnc (IdleAnim) считает, что краны движутся и в простое.
- Спорные места для прототипа: читаются ли «строится» и «готово» издалека; связан ли кран с очередью производства.
