# Ремонтный цех — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 4×3 клеток · Роль: поддержка (ремонт техники)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Открытая бетонная площадка 4×3 с ремонтными манипуляторами сбоку: по ней видно, куда заезжать на ремонт. Ремонтная клетка смещена от центра здания, поэтому точка ремонта читается не в середине. Манипуляторы и оборудование у нас свои, без копии оригинала.

## Требования к этой модели

- **Размер:** 4×3 клеток = 4×3 единиц Godot.
- **Высота:** площадка низкая, манипуляторы примерно до высоты танка (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** два ремонтных манипулятора (предложение: в `animations.md` движущиеся части не названы, движение не подтверждено).
- **Узлы в Blender:** клипы `open` (стадия 1), `active` (стадия 2, цикл), `close` (стадия 3) для `AnimationPlayer` (предложение); `damage_*` (дым до 50% HP, огонь и густой дым до 25%; предложение); шейдер `build_up` (постройка и продажа); метка `repair_cell` (в README это поле данных, не узел модели).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стенке площадки, напротив манипуляторов (предложение; цвет игрока на здании не подтверждён, `appearance.md`).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сохранить видимую ремонтную клетку, по ней игрок понимает, куда заезжать; манипуляторы и оборудование сделать своими.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A low open concrete repair pad with two repair arms on one side and a repair spot set off-center on the pad, the arms about tank height. Standing on a flat rectangular base plate matching a 4×3 footprint. Dark steel and concrete with a muted red accent. The two repair arms are separate parts. One flat panel in pure magenta #FF00FF on a side wall, opposite the arms. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A low open concrete repair pad with two repair arms on one side and a repair spot set off-center on the pad, the arms about tank height. Standing on a flat rectangular base plate matching a 4×3 footprint. Dark steel and concrete with a muted red accent. The two repair arms are separate parts. One flat panel in pure magenta #FF00FF on a side wall, opposite the arms. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A low open concrete repair pad with two repair arms on one side and a repair spot set off-center on the pad, the arms about tank height. Standing on a flat rectangular base plate matching a 4×3 footprint. Dark steel and concrete with a muted red accent. The two repair arms are separate parts. One flat panel in pure magenta #FF00FF on a side wall, opposite the arms. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: два манипулятора ремонта (имена — предложение, в `animations.md` не заданы), корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Число и расположение манипуляторов: README говорит «сбоку», в источниках это не подтверждено (`appearance.md`, «Материалы и цвета»). Два манипулятора и их вид — предложение.
  - Какие части движутся (манипуляторы, искры, огни), не описано (`animations.md`, «Действия»). Поэтому отдельные объекты манипуляторов — предложение.
  - Что показывают три ремонтные стадии: ModEnc описывает их для сервисного депо вообще, для внутреннего имени NADEPT страницы не называют (`animations.md`, «Ремонт техники»).
  - Поза покоя и вид простоя (`IdleAnim`) не описаны (`animations.md`, «Простой»; `appearance.md`, «Анимация»).
  - Вид при нехватке энергии не подтверждён: у здания нет флага `Powered`, и ремонт при дефиците в игре не сверен (README, «Механики»; `animations.md`, «Без энергии»).
  - Высота относительно юнитов не подтверждена (`appearance.md`, «Силуэт и размер»); значение в промпте — предложение.
- Спорные места, которые проверим в прототипе:
  - Читаются ли три стадии, когда у цеха в очереди несколько машин (`animations.md`, «Для нашей версии»; `appearance.md`, «Для нашей версии»).
  - Совпадает ли поза манипуляторов с машиной на смещённой ремонтной клетке (`animations.md`, «Для нашей версии»).
  - Что показывать, если юнит ушёл или телепортирован до конца ремонта: поведение стадии `close` в источниках не описано (`animations.md`, «Для нашей версии»).
