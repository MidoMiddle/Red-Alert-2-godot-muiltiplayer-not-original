# Ракетная шахта — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 3×3 клеток · Роль: супероружие (ударное)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Бетонный ангар 3×3 высотой около двух танков с двумя створками наверху: по ним читается пусковая установка, а не склад. В нашей версии форма ракеты и дымный след свои; галерейные рисунки не используем.

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot.
- **Высота:** около двух высот танка (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** две створки (клипы `open` и `close`) и ракета — узел `missile` (предложение в `animations.md`).
- **Узлы в Blender:** `smoke_*` у среза шахты (выхлоп при старте; предложение), `light_*` (индикатор готовности; предложение), `damage_*` (дым; огонь и густой дым при сильном повреждении), клипы `open`, `active`, `close` (предложение). `spin_*` не нужны.
- **Цвет команды:** панель `#FF00FF` на фасаде рядом со створками.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение) — по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** двухстворчатый ангар и открытие створок перед пуском сохраняем: по ним читается «скоро удар»; форму ракеты и след дыма делаем своими.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A reinforced concrete launch hangar for a long-range strike weapon, about two tank heights tall. Two large blast doors cover its roof, with visible seams, and the doors are separate parts. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. Heavy concrete walls with thick ribs and a few exhaust vents. One flat panel in pure magenta #FF00FF on the front wall beside the doors. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A reinforced concrete launch hangar for a long-range strike weapon, about two tank heights tall. Two large blast doors cover its roof, with visible seams, and the doors are separate parts. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. Heavy concrete walls with thick ribs and a few exhaust vents. One flat panel in pure magenta #FF00FF on the front wall beside the doors. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A reinforced concrete launch hangar for a long-range strike weapon, about two tank heights tall. Two large blast doors cover its roof, with visible seams, and the doors are separate parts. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. Heavy concrete walls with thick ribs and a few exhaust vents. One flat panel in pure magenta #FF00FF on the front wall beside the doors. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус, две створки, ракета (`missile`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Высота не подтверждена (`appearance.md`); значение «около двух высот танка» — предложение. Цвет игрока не подтверждён; зону `#FF00FF` выбрали нами.
  - Форма ангара: `appearance.md` называет его «длинным» со ссылкой на `README.md`, но в README сказано только «бетонный ангар с двумя створками». По C&C Wiki — «вытянутое строение, похожее на высокий ангар». C&C Wiki недоступна (402/403), поэтому форма в промпте — предложение.
  - Расположение створок наверху — наше предложение: источники не описывают, откуда выходит ракета. В покое створки закрыты (предложение, `animations.md`), прямо это не описано (`appearance.md`).
  - Как двигаются створки (раздвигаются или откидываются), когда играет `close`, и длительности клипов `open`, `active`, `close`: не подтверждено. В `README.md` и `appearance.md` секунд нет (`animations.md`).
  - Пассивный и заряженный виды: отличие не описано (`appearance.md`, `animations.md`).
  - Вид при нехватке энергии и судьба ракеты при разрушении во время пуска не описаны (`animations.md`).
  - Разрушение и продажа: анимации в галерее нет (`appearance.md`). Взрыв и обломки — предложение.
- Спорные места, которые проверим в прототипе:
  - Успевает ли игрок заметить `open` и `active` с дальнего плана, и какие длительности дают понятный, но не затянутый пуск (`appearance.md`, `animations.md`).
  - Чем заряженный вид отличается от пассивного, чтобы «готово» читалось без интерфейсного таймера (`animations.md`).
  - Что делать с клипами, если энергия пропала, шпион сбросил таймер или шахту разрушили во время пуска (`animations.md`).
