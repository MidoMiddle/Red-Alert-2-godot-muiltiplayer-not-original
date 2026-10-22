# Казармы — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 2×2 клеток · Роль: производство (пехота)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Здание для обучения пехоты: компактный корпус 2×2 с высокой мачтой на крыше, по которой его узнают на карте. В нашей версии вместо фигуры на крыше стоит мачта с плоским знаменем, которое окрашивается в цвет команды, а корпус выполнен в палитре Союза.

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot.
- **Высота:** около полутора танков до крыши, мачта выше (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание.
- **Узлы в Blender:** `light_*` (индикатор обучения), `damage_*` (два узла: у корпуса и у мачты), маркер точки выхода `exit_infantry` (пример имени из `animations.md`). Предложение, см. `animations.md`.
- **Цвет команды:** плоское знамя на мачте; в референсе окрашено `#FF00FF`.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение). См. [гайд, раздел 4](../../generation_guide.md).
- **Особенности:** вертикальный акцент на крыше выделяет здание среди зданий 2×2 (сохраняем); сюжет акцента свой: мачта со знаменем вместо статуи, фигуру из галереи не копируем (делаем своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact training building where infantry recruits are produced. Narrow concrete block about one and a half tanks tall to the roof, with a tall vertical steel mast on top as its main silhouette mark. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. The mast is a separate detail with a plain square section. One flat banner in pure magenta #FF00FF on the mast. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact training building where infantry recruits are produced. Narrow concrete block about one and a half tanks tall to the roof, with a tall vertical steel mast on top as its main silhouette mark. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. The mast is a separate detail with a plain square section. One flat banner in pure magenta #FF00FF on the mast. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact training building where infantry recruits are produced. Narrow concrete block about one and a half tanks tall to the roof, with a tall vertical steel mast on top as its main silhouette mark. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. The mast is a separate detail with a plain square section. One flat banner in pure magenta #FF00FF on the mast. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему (из `appearance.md` и `animations.md`):
  - высота относительно юнитов: в README её нет, в `appearance.md` «не подтверждена»; значение выше — предложение;
  - вид в покое, при обучении и при выходе юнитов: ключ анимации производства для здания не назван, дверь и створка не описаны; источники дают только анимацию постройки (`animations.md`);
  - состояния без энергии, повреждение (до ~50% и ~25% HP), разрушение и продажа не описаны (`appearance.md`, `animations.md`); выходят ли выжившие из разрушенного здания, не подтверждено;
  - флаги анимации без энергии для этого здания не сверены: страницы источников расходятся (`animations.md`).
- Спорные места, которые проверим в прототипе:
  - читается ли мачта со знаменем рядом с другими зданиями 2×2 и при дальнем плане камеры (`appearance.md`);
  - читается ли начало и конец обучения по `light_*` и по появлению юнита; подходит ли длительность `build_up` к постройке около 0:20 по README (`animations.md`);
  - не закрывают ли огонь и дым у `damage_*` вертикальный акцент (`animations.md`);
  - держит ли генератор цвет `#FF00FF` на узком знамени (гайд, раздел 4);
  - нужна ли створка с клипами `open` и `close`: только если в модели будет отдельная дверь (`animations.md`).
