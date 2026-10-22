# Военный завод — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 5×3 клеток · Роль: производство (техника и авиация)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Длинный цех для выпуска техники: широкие ворота на фасаде и раздвижная крыша для летающей техники. По силуэту читается выезд для тяжёлой техники. В нашей версии ворота и крыша свои, а нужна ли крыша вообще, решит прототип.

## Требования к этой модели

- **Размер:** 5×3 клеток = 5×3 единиц Godot (рабочее значение). В таблице README 5×3; заметки README дают 3×4 для нашей версии.
- **Высота:** около двух с половиной танков; ворота выше танка (предложение).
- **Подвижные части (отдельные объекты):** ворота (узел `gate`) и раздвижная крыша (узел `roof`); предложение.
- **Узлы в Blender:** клипы `open` и `close` на узлах `gate` и `roof`, `damage_*` (дым и искры при повреждении), `debris_*` (обломки при разрушении). Предложение, см. `animations.md`.
- **Цвет команды:** плоская панель на фасаде рядом с воротами; в референсе окрашена `#FF00FF`.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение). См. [гайд, раздел 4](../../generation_guide.md).
- **Особенности:** крупный выезд для тяжёлой техники, по которому читается тяжёлый завод (сохраняем); решение для авиации наше: отдельная верхняя створка или её отказ (делаем своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A large vehicle production hall for heavy ground and air units. Hall about two and a half tanks tall, with a wide front gate taller than a tank and a sliding roof panel. It stands on a flat rectangular base plate matching a 5×3 footprint. Dark steel and concrete with a muted red accent. The gate and the roof panel are separate moving parts. One flat panel in pure magenta #FF00FF on the front wall beside the gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A large vehicle production hall for heavy ground and air units. Hall about two and a half tanks tall, with a wide front gate taller than a tank and a sliding roof panel. It stands on a flat rectangular base plate matching a 5×3 footprint. Dark steel and concrete with a muted red accent. The gate and the roof panel are separate moving parts. One flat panel in pure magenta #FF00FF on the front wall beside the gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A large vehicle production hall for heavy ground and air units. Hall about two and a half tanks tall, with a wide front gate taller than a tank and a sliding roof panel. It stands on a flat rectangular base plate matching a 5×3 footprint. Dark steel and concrete with a muted red accent. The gate and the roof panel are separate moving parts. One flat panel in pure magenta #FF00FF on the front wall beside the gate. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 5×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, ворота (`gate`), крыша (`roof`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему (из `appearance.md` и `animations.md`):
  - высота относительно юнитов: в README её нет, в `appearance.md` «не подтверждена»; значение выше — предложение;
  - анимация ворот и раздвижной крыши в источниках не описана; клипы `open` и `close` и узлы `gate` и `roof` — предложение (`animations.md`);
  - подходит ли летающая техника под условие выхода через крышу, не сверено; источники расходятся про слои крыши, для 3D это не блокирует (`animations.md`);
  - покой, нехватка энергии, повреждение, разрушение и продажа не описаны (`appearance.md`); выходят ли выжившие при разрушении, не подтверждено.
- Спорные места, которые проверим в прототипе:
  - хватает ли ширины ворот и времени `open` для самой крупной техники; совпадает ли выезд с точкой выхода, которая в YR сдвинута (README, `animations.md`);
  - читается ли раскрытие крыши и вылет летающей техники при разных углах камеры; нужна ли крыша вообще или хватит выхода «вверх» (README, `appearance.md`);
  - что происходит с воротами и крышей, если завод продан, захвачен или уничтожен во время выхода техники (`animations.md`).
