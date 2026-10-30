# 20. Спортплощадка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Спортплощадка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса, моделей и текстур спортплощадки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_sports_goal` | объект | 1x1 | 2 | ≤ 500 | нет | нет |
| `city_sport_grass` | текстура, бесшовная | — | — | — | — | — |
| `city_sport_lines` | декаль с прозрачностью | — | — | — | — | — |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small city football pitch in neutral daylight: a rectangular grass field with white painted lines, two white goal frames with netting at the ends, and a low grey wire-mesh fence around it. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated scene on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель и текстуры

### 3.1. `city_sports_goal` (модель, Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

```text
A white metal football goal frame about one unit wide and two units tall, with a net on the back, two posts and a crossbar, slightly weathered paint. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `city_sport_grass` (текстура, генератор изображений)

```text
Seamless tileable texture of a mown football pitch grass with faint darker stripes from mowing and a few worn patches, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.3. `city_sport_lines` (декаль, генератор изображений)

```text
Pitch markings decal on a transparent background: white painted lines of a football pitch, a centre line, a centre circle and penalty boxes, thin and even, square image, no text.
```

Фон вырезаем после генерации.

## 4. Наши требования

- **Модель стойки:** пивот — центр клетки на земле. Узлы: `frame`, `net`. Цвета команды нет.
- **Бюджет** ≤ 500 треугольников, текстуры 1024².
- **Поле — не модель.** Геометрии у поля нет: это текстура и декаль разметки.
- **Не блокирует** — это данные карты, в модели не отражается.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_sports_goal.glb` | Blender, скрипт `tools/blender/families/city_sports_field.py` | 2026-10-30 | готово |
| `city_sports_goal_damaged.glb` | то же | 2026-10-30 | готово |
| `city_sports_goal_ruins.glb` | то же | 2026-10-30 | готово |
| `city_sport_grass.png` | то же (numpy) | 2026-10-30 | готово |
| `city_sport_lines.png` | то же (numpy) | 2026-10-30 | готово |

Всё сделано скриптом, без генераторов. Ворота: узлы `frame` (белая сталь, запечена) и `net` (сетка отдельным материалом с маленькой картинкой ячейки и прозрачностью между нитями, не запекается). Ворота в размере, в котором их рисует игра: 0,9 в ширину и 0,45 в высоту, чтобы уместиться в клетку, а не 2. Три состояния по [appearance.md](appearance.md): целые, повреждённые (стойка согнута, перекладина провисла, правая половина задней сетки оторвана) и разрушенные (смяты к земле, рама перекошена, сетка обвисла). Трава — бесшовная текстура 1024² в `game/assets/textures/terrain/`: полосы от косилки, зерно, вытоптанные пятна. Разметка — декаль 1024² с прозрачным фоном в `game/assets/textures/decals/`: края поля, центральная линия и круг, штрафные площади. В галерее Blender поле лежит в ряду «10. Город»: трава и разметка на квадратной плоскости 3×3. В игре всё это пока некуда подключить: объекта карты для ворот нет, а поле — текстура карты.

## Открытые вопросы

1. Нужна ли асфальтовая версия текстуры (см. [appearance](appearance.md)).
