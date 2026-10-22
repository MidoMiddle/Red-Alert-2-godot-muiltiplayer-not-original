# Очистительный завод — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 4×3 клеток · Роль: экономика
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Экономическое здание: широкий низкий корпус с конвейером разгрузки и трубами. По силуэту видно, куда возвращается комбайн и где руда превращается в деньги. В нашей версии конвейер и трубы свои, а здание не копирует оригинал.

## Требования к этой модели

- **Размер:** 4×3 клеток = 4×3 единиц Godot (рабочее значение). В таблице README 4×3; заметки README для нашего проекта дают 3×3.
- **Высота:** около одного танка, трубы выше (предложение).
- **Подвижные части (отдельные объекты):** лента конвейера (клип `active`; предложение).
- **Узлы в Blender:** клип `active` (лента конвейера), `smoke_*` (дым над трубами), `light_*` (мигание на время разгрузки), `damage_*` (два-три узла), `debris_*` (обломки при разрушении). Предложение, см. `animations.md`.
- **Цвет команды:** плоская панель у конвейера; в референсе окрашена `#FF00FF`.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение). См. [гайд, раздел 4](../../generation_guide.md).
- **Особенности:** видимая точка разгрузки (конвейер), по ней игрок понимает, куда вернётся комбайн (сохраняем); форма конвейера, трубы и дым свои (делаем своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
An economy building that turns collected ore into money. Wide, low industrial block about one tank tall, with a steel conveyor ramp on the front and two chimney stacks above. It stands on a flat rectangular base plate matching a 4×3 footprint. Dark steel and concrete with a muted red accent. The conveyor belt is a separate moving part, and the stacks are separate details. One flat panel in pure magenta #FF00FF beside the conveyor. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
An economy building that turns collected ore into money. Wide, low industrial block about one tank tall, with a steel conveyor ramp on the front and two chimney stacks above. It stands on a flat rectangular base plate matching a 4×3 footprint. Dark steel and concrete with a muted red accent. The conveyor belt is a separate moving part, and the stacks are separate details. One flat panel in pure magenta #FF00FF beside the conveyor. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
An economy building that turns collected ore into money. Wide, low industrial block about one tank tall, with a steel conveyor ramp on the front and two chimney stacks above. It stands on a flat rectangular base plate matching a 4×3 footprint. Dark steel and concrete with a muted red accent. The conveyor belt is a separate moving part, and the stacks are separate details. One flat panel in pure magenta #FF00FF beside the conveyor. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 4×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, лента конвейера
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему (из `appearance.md` и `animations.md`):
  - высота относительно юнитов: в README её нет, в `appearance.md` «не подтверждена»; значение выше — предложение;
  - трубы и дым: только по README; `appearance.md` и `animations.md` их не подтверждают;
  - вид ленты при разгрузке и клипы самого здания: источники их не называют; клип `active` на ленте — предложение (`animations.md`);
  - выход бесплатного комбайна: проём и ворота не описаны (`animations.md`);
  - покой, нехватка энергии, повреждение, разрушение и продажа не описаны (`appearance.md`, `animations.md`).
- Спорные места, которые проверим в прототипе:
  - размер: README даёт 4×3, а его заметки для нашего проекта — 3×3; нужно решить, где клетка разгрузки (README, `animations.md`);
  - длина клипа `active` против времени разгрузки и поведение клипа, если комбайн отозван вручную (`animations.md`);
  - читаются ли конвейер и дым издалека и не перекрывает ли их другая техника (`appearance.md`); очередь комбайнов тоже может закрыть вид (`animations.md`).
