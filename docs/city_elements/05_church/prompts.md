# 05. Церковь: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Церковь](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели церкви. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_church` | здание | 2x3 | 5 (колокольня), 2,5 (корпус) | ≤ 10 000 | да, 4 | да (полоса на колокольне) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small church with a bell tower on a city street in neutral daylight: white plastered walls, a steep dark tiled roof, a tall brick bell tower with clock faces, arched windows, and a low fence around the yard. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_church`

```text
A small church with white plastered walls, a steep dark tiled roof and arched windows, and a tall brick bell tower with a clock face on each side and a narrow colour band near its top. A low fence around the yard. No symbols or lettering on the walls. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x3`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `tower` (колокольня, отдельно для состояния «обломок колокольни» в руинах), `team_mask` (полоса на колокольне).
- **Гарнизон 4.** Бойцы в окнах нефа — код.
- **Колокольня** — самый высокий силуэт в городе. Высота 5 клеток, не выше.
- **Двор и ограда** — секции из [08_fences_walls](../../map_elements/08_fences_walls/prompts.md).
- **Без религиозных символов и надписей.** Крест и эмблемы не делаем: это стиль без привязки к реальным символам.
- **Бюджет** ≤ 10 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_church.glb` | Blender, скрипт `tools/blender/families/city_church.py` | 2026-10-29 | готово |
| `city_church_occupied.glb` | то же | 2026-10-29 | готово |
| `city_church_damaged.glb` | то же | 2026-10-29 | готово |
| `city_church_damaged_occupied.glb` | то же | 2026-10-29 | готово |
| `city_church_abandoned.glb` | то же | 2026-10-29 | готово |
| `city_church_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Колокольня высотой 5, корпус до конька 2,5. Колокольня — отдельный узел `tower`, в руинах от неё остаётся обломок (открытый вопрос 2 решён так). Цвет владельца — весь шатёр колокольни, а не полоса: игра сжимает здание по высоте, и узкая полоса перестаёт читаться. Крестов, символов и надписей нет. Двора и ограды в модели нет. В игре церковь — `neutral_church` (`game/data/neutral/civilian.yaml`); модели подключены к ней в `game/data/visuals/models.yaml` со всеми состояниями. В данных вместимость гарнизона 6, а в модели стволов 4, по этому документу.

## Открытые вопросы

1. Делать ли церковь неразрушаемой, чтобы она оставалась ориентиром до конца партии.
2. Оставлять ли колокольню в руинах, чтобы ориентир не терялся (см. узел `tower`).
