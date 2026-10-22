# Танковый бункер — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 2×2 клетки · Роль: оборона (гарнизон для техники)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Укреплённая площадка для одной машины: когда танк заезжает, вокруг поднимаются стены, и он работает как огневая точка. Сам бункер не стреляет. В силуэте читаются низкие стены и пустой подиум, а в занятом виде — поднятые стены.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение). В README: «2×2 или больше»; по ModEnc основание 1×1 не работает. Точное значение не подтверждено.
- **Высота:** в опущенном виде низкие стены, в поднятом — выше танка (предложение).
- **Подвижные части (отдельные объекты):** стены — отдельные объекты, по одному на сторону подиума (предложение; число не подтверждено). Подиум с основанием — одна часть.
- **Узлы в Blender:** `open` и `close` для стен (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на передней стене (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Поднятые стены при занятом состоянии — понятный признак, что танк защищён. Сохраняем.
  2. Стены и подиум делаем своей формы. Проверить, не перекрывают ли поднятые стены соседние клетки.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A reinforced single-vehicle emplacement: an empty square pad surrounded by low wall panels. Low wall panels at rest; they rise taller than a tank when a vehicle drives in. On a flat rectangular base plate matching a 2×2 footprint, dark violet armoured plating with an organic-industrial texture and a cool glowing accent along the top edges. The wall panels are separate parts. One flat panel in pure magenta #FF00FF on the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A reinforced single-vehicle emplacement: an empty square pad surrounded by low wall panels. Low wall panels at rest; they rise taller than a tank when a vehicle drives in. On a flat rectangular base plate matching a 2×2 footprint, dark violet armoured plating with an organic-industrial texture and a cool glowing accent along the top edges. The wall panels are separate parts. One flat panel in pure magenta #FF00FF on the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A reinforced single-vehicle emplacement: an empty square pad surrounded by low wall panels. Low wall panels at rest; they rise taller than a tank when a vehicle drives in. On a flat rectangular base plate matching a 2×2 footprint, dark violet armoured plating with an organic-industrial texture and a cool glowing accent along the top edges. The wall panels are separate parts. One flat panel in pure magenta #FF00FF on the front wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: подиум с основанием (одна часть); стены (отдельные объекты)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Референс — пустой бункер** со стенами в опущенном виде. Положение стен в пустом виде не подтверждено (`appearance.md`), это предложение.
- **Происхождение.** Здание советское: иконка с танком, внутреннее имя с префиксом `NA`, README. README ставит открытый вопрос: нужна ли эта механика Культу или её лучше отдать Союзу. Решить до пайплайна.
- **Танк внутри** — отдельная модель, в промпт не входит (поэтому «без техники»).
- Размер: README «2×2 или больше», точное значение не подтверждено. Рабочее значение 2×2 (советский боевой бункер для пехоты тоже 2×2).
- Не подтверждено: форма и высота стен в опущенном и поднятом виде, вид в покое, визуал при разрушении (в том числе танка внутри), визуал повреждения и продажи.
- Проверим в прототипе: виден ли танк внутри при поднятых стенах; заезд без заикания (ModEnc, `Bunker`) и опускание стен при выходе.
