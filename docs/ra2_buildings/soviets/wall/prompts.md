# Стена — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 1×1 клетка · Роль: оборона (пассивная преграда)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Одиночный бетонный сегмент 1×1, который перекрывает проход пехоте и технике. В силуэте читается сплошная серая преграда без действий и без окраски игрока. В нашей версии это самая дешёвая постройка: её ставят протяжкой, а здесь описан один сегмент без соединений.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot.
- **Высота:** примерно в рост стоящего пехотинца (предложение). В README высоты нет; в `appearance.md` высота «не подтверждена».
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` движущихся частей и действий нет).
- **Узлы в Blender:** `damage_*` (лёгкая пыль, без огня; предложение из `animations.md`), `debris_*` (обломки бетона и пыль при разрушении, без взрыва; предложение из `animations.md`).
- **Цвет команды:** в референсе окрашена `#FF00FF` торцевая грань сегмента (наш выбор: в `appearance.md` цвета игрока нет, а маркировку на торце там предлагают).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** нейтральный серый цвет без окраски игрока, стена читается как препятствие (сохранить); фракционная маркировка на торце, а не цвет игрока; галерейные спрайты не копируем (сделать своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A single concrete wall segment that blocks infantry and vehicles, about one standing soldier tall, a solid block with a plain top edge. The segment is one solid part with no moving parts. It sits on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the end face of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single wall segment with no connections, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A single concrete wall segment that blocks infantry and vehicles, about one standing soldier tall, a solid block with a plain top edge. The segment is one solid part with no moving parts. It sits on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the end face of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single wall segment with no connections, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A single concrete wall segment that blocks infantry and vehicles, about one standing soldier tall, a solid block with a plain top edge. The segment is one solid part with no moving parts. It sits on a flat rectangular base plate matching a 1×1 footprint. Dark steel and concrete with a muted red accent. One flat panel in pure magenta #FF00FF on the end face of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single wall segment with no connections, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Не подтверждено:**
  - высота относительно юнитов (`appearance.md`: не подтверждена); в промпте значение предложенное;
  - протяжка: README и `appearance.md` (со ссылкой на C&C Wiki) дают промежуток до 4 клеток, а `animations.md` (со ссылкой на cncnz) пишет «до 4 сегментов за раз». Правило заполнения и оплаты не подтверждено (README);
  - анимация постройки и покоя, вид при разрушении не описаны (`appearance.md`, `animations.md`); число ступеней повреждения для `NAWALL` не подтверждено (`DamageLevels`, `animations.md`);
  - HP 300 из README: `animations.md` указывает, что у стены-оверлея по ModEnc нет обычного здоровья; пороги 50% и 25% для ступеней повреждения — предложение.
- **Спорные места, которые проверим в прототипе:**
  - **Открытый вопрос: соединения сегментов.** Стыки, углы, Т-образные сегменты и ворота здесь не решаем: промпты описывают один сегмент без соединений. Как собрать варианты по соседям (`animations.md` предлагает набор вариантов) и нужны ли ворота (в источниках не описаны), решим отдельно;
  - акцент: общая палитра даёт красный акцент, а `appearance.md` пишет «акценты: нет» и серый бетон. Промпт следует общей палитре; нужен ли акцент на стене, проверим;
  - нужна ли маска команды на стене, если цвета игрока нет (`appearance.md`);
  - сколько ступеней повреждения нужно и читаются ли они с игровой камеры (`animations.md`);
  - протяжка: появляются ли сегменты разом или по очереди и хватает ли им времени `build_up` (`animations.md`).
