# 26. Автомастерская: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Автомастерская](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели автомастерской. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_auto_repair` | здание | 2x2 | 3 | ≤ 6 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small city car repair garage in neutral daylight: a low grey block building with two wide roll-up bay doors, a plain sign board above them without text, a flat roof with a vent. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_auto_repair`

```text
A low city car repair garage about two units wide, two units deep and three units tall: grey block walls, two wide roll-up bay doors, a plain sign board without text above the doors, a flat roof with a vent. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `bay_doors` (створки ворот боксов, отдельные узлы для открытия), `sign` (вывеска без текста).
- **Гарнизона нет.** Проёмы боксов код не использует.
- **Бюджет** ≤ 6 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_auto_repair.glb` | Blender, скрипт `tools/blender/families/city_auto_repair.py` | 2026-10-30 | готово |
| `city_auto_repair_damaged.glb` | то же | 2026-10-30 | готово |
| `city_auto_repair_abandoned.glb` | то же | 2026-10-30 | готово |
| `city_auto_repair_ruins.glb` | то же | 2026-10-30 | готово |

Модель сделана скриптом, без генератора. Один высокий этаж боксов до крыши 1,6, с парапетом 1,74, на задней половине футпринта; впереди бетонная площадка с двумя машинами. Узлы `body`, `roof`, `bay_doors_left` и `bay_doors_right` (ворота боксов, отдельно для открытия), `sign`, `cars`, `base`. Машины пока в модели, отдельным узлом `cars` (открытый вопрос 1): если их сделают отдельными объектами, узел можно убрать. Они сделаны выше, чем есть, на обратную величину сжатия (1 / 0,35), чтобы после сжатия по высоте выглядеть как машины. Гарнизона нет, поэтому состояний четыре, как у кафе: целая, повреждённая, покинутая (горит: копоть, масляное пятно, сгоревшие машины, дым из вентиляции — точка `fire_2`) и руины. В игре мастерская — `neutral_auto_repair` (`game/data/neutral/civilian.yaml`); модели подключены к ней в `game/data/visuals/models.yaml` со всеми состояниями.

## Открытые вопросы

1. Машины у входа: в модели или отдельными объектами, см. [appearance](appearance.md), вопрос 1.
