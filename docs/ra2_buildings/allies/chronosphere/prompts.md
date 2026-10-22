# Хроносфера — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 4×3 клетки · Роль: супероружие (перенос армии)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Хроносфера — супероружие, которое переносит группу наземной техники или кораблей в выбранную точку; здание — купол на бетонном основании. В силуэте читается купол, а заряженное состояние должно быть видно по свету. В нашей версии облик свой: форма купола и эффект переноса наши; эффект должен показывать источник и точку назначения.

## Требования к этой модели

- **Размер:** 4×3 клетки = 4×3 единицы Godot (рабочее значение: по порту OpenRA не подтверждено).
- **Высота:** купол около 3 единиц над низким постаментом (предложение; форма купола в источниках не описана).
- **Подвижные части (отдельные объекты):** купол — отдельный объект (предложение), клип `active` на время переноса (предложение). Остальное — одна часть и основание.
- **Узлы в Blender:** `light_charge` — свечение заряженного состояния (предложение); клип `active` на куполе (предложение); `damage_*` (предложение); `debris_*` (предложение). Эффекты источника и точки переноса — отдельные объекты на поле, не узлы здания.
- **Цвет команды:** кольцевая полоса `#FF00FF` вокруг бетонного основания (предложение: цвет игрока не подтверждён; захват невозможен).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа — бетон по README, не сверено в игре.
- **Особенности:** 1) сохранить видимый сигнал «заряжено»: по нему игрок понимает, что супероружие готово; 2) форму купола и эффект переноса сделать своими; эффект должен показывать источник и точку назначения.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A large domed teleportation chamber, a superweapon building that relocates a group of ground units to a chosen point on the map. It stands on a concrete base plate matching a 4×3 footprint, the dome about three units tall above a low plinth. Light brushed metal segments, off-white panels, tinted glass dome panels and cool blue charge lights. The dome is a separate part that can glow. One flat ring strip in pure magenta #FF00FF around the concrete base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A large domed teleportation chamber, a superweapon building that relocates a group of ground units to a chosen point on the map. It stands on a concrete base plate matching a 4×3 footprint, the dome about three units tall above a low plinth. Light brushed metal segments, off-white panels, tinted glass dome panels and cool blue charge lights. The dome is a separate part that can glow. One flat ring strip in pure magenta #FF00FF around the concrete base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A large domed teleportation chamber, a superweapon building that relocates a group of ground units to a chosen point on the map. It stands on a concrete base plate matching a 4×3 footprint, the dome about three units tall above a low plinth. Light brushed metal segments, off-white panels, tinted glass dome panels and cool blue charge lights. The dome is a separate part that can glow. One flat ring strip in pure magenta #FF00FF around the concrete base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание, купол (`active`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 4×3 — по порту OpenRA (рабочее значение); форма и высота купола (appearance.md); пассивный, заряженный и повреждённый виды взяты из галереи вики: appearance.md помечает их «подтверждено подписями», animations.md — «вики, не проверено»; эффекты переноса в галерее не описаны; неиспользуемая анимация разрушения — вики, не проверено.
- Спорные места для прототипа: читаются ли три вида без подписи; совпадает ли пауза 60 кадров (README) с длительностью эффекта переноса.
