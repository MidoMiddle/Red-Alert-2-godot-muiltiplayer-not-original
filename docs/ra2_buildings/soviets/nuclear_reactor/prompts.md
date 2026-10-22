# Ядерный реактор — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 4×4 клеток · Роль: энергия (поздняя игра)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Крупный бетонный комплекс 4×4 с двумя градирнями: по силуэту видно тяжёлую энергетику, заметно крупнее обычной электростанции. Градирни и цвет свечения у нас свои (предложение), описание README без проверки не копируем. Повреждения в модель не вписываем: они идут узлами `damage_*`.

## Требования к этой модели

- **Размер:** 4×4 клеток = 4×4 единиц Godot.
- **Высота:** основной корпус примерно в два раза выше танка, градирни примерно в три (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание; движущихся частей и ворот в `animations.md` нет.
- **Узлы в Blender:** шейдер `build_up` (постройка и продажа); `damage_*` (огонь в двух точках и серый дым до 50% HP — по rules; до 25% сильнее, предложение); `light_core` (пульсация свечения, предложение); `smoke_tower_*` (пар градирен, предложение); `debris_*` при разрушении (предложение).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стенке главного корпуса (предложение; цвет игрока на здании не подтверждён, `appearance.md`).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сохранить крупный силуэт, который отличает тяжёлую энергетику от обычной; градирни и цвет свечения сделать своими.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A large concrete power generation complex with two tall cooling towers and a faintly glowing core housing, the main block about twice a tank's height, the towers about three times. Standing on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. No moving parts; the cooling towers and core housing are fixed. One flat panel in pure magenta #FF00FF on a side wall of the main block. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A large concrete power generation complex with two tall cooling towers and a faintly glowing core housing, the main block about twice a tank's height, the towers about three times. Standing on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. No moving parts; the cooling towers and core housing are fixed. One flat panel in pure magenta #FF00FF on a side wall of the main block. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A large concrete power generation complex with two tall cooling towers and a faintly glowing core housing, the main block about twice a tank's height, the towers about three times. Standing on a flat rectangular base plate matching a 4×4 footprint. Dark steel and concrete with a muted red accent. No moving parts; the cooling towers and core housing are fixed. One flat panel in pure magenta #FF00FF on a side wall of the main block. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×4 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Цвет свечения: README называет его зелёным, но в `appearance.md` это не подтверждено. В промпте свечение без цвета; цвет — предложение.
  - Градирни: по README, не сверено в игре (`appearance.md`, «Ключевые детали»). Высота — предложение.
  - Вид активной анимации (`ActiveAnim`, около 11 кадров по artmd.ini) и пар градирен не описан. `light_core` и `smoke_tower_*` — предложение (`animations.md`, «Действия»).
  - Вид взрыва и обломков не подтверждён. Флаг `Crewed=yes` указан в `animations.md` по ModEnc и не сверён; в README про экипаж не сказано (`animations.md`, «Повреждение и разрушение»; README, «Прочее»).
  - Вид при ~25% HP: ключей для красной стадии у реактора нет, вид не подтверждён; порог 25% проектный (`animations.md`, «Повреждение и разрушение»).
  - Высота относительно юнитов не подтверждена (`appearance.md`, «Силуэт и размер»); значения в промпте — предложение.
- Спорные места, которые проверим в прототипе:
  - Читается ли работающий реактор без звука гула, который в README слышен при работе (`animations.md`, «Для нашей версии», пункт 1).
  - Читается ли опасность здания при ~25% HP и нужен ли отдельный сигнал владельцу и противнику (`animations.md`, «Для нашей версии», пункт 2; `appearance.md`, «Для нашей версии»).
  - Заметно ли замирание при сливе энергии тарелкой Юрия или блэкауте в YR (`animations.md`, «Для нашей версии», пункт 3; «Без энергии»).
