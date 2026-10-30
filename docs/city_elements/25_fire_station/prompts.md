# 25. Пожарная станция: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Пожарная станция](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели пожарной станции. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_fire_station` | здание | 3x2 | 6 | ≤ 10 000 | да, 4 | да (полоса над воротами) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a city fire station in neutral daylight: a red brick two-storey building with two large red roller doors for fire engines, a tall hose-drying tower with narrow windows on one side, a flat roof with a short antenna. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_fire_station`

```text
A city fire station about three units wide, two units deep and six units tall: red brick walls, two large red roller doors for fire engines, a tall hose-drying tower with narrow windows on one side, rows of small windows above the doors, a narrow colour band above the doors. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `doors_left` и `doors_right` (ворота, отдельные узлы для открытия), `tower` (башня), `garrison_windows` (окна казармы), `team_mask` (полоса над воротами).
- **Гарнизон 4:** окна видны в модели, на них опирается код.
- **Бюджет** ≤ 10 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_fire_station.glb` | Blender, скрипт `tools/blender/families/city_fire_station.py` | 2026-10-30 | готово |
| `city_fire_station_occupied.glb` | то же | 2026-10-30 | готово |
| `city_fire_station_damaged.glb` | то же | 2026-10-30 | готово |
| `city_fire_station_damaged_occupied.glb` | то же | 2026-10-30 | готово |
| `city_fire_station_abandoned.glb` | то же | 2026-10-30 | готово |
| `city_fire_station_ruins.glb` | то же | 2026-10-30 | готово |

Модель сделана скриптом, без генератора. Высокий этаж гаража и этаж казармы до крыши 3,5, башня для сушки шлангов в правом переднем углу до 6,05. Узлы `body`, `doors_left`, `doors_right` (ворота закрыты, открытый вопрос 1: отдельные узлы позволят их открыть), `tower`, `garrison_windows` (окна казармы), `team_mask`, `base`. Цвет владельца — широкая полоса над воротами и парапет на верху башни: игра сжимает здание по высоте, и узкая полоса одна перестаёт читаться. В повреждённой станции левые ворота сорваны и привалены к стене, правые выгнуты; в покинутой башня обломилась над крышей. Гарнизон: четыре ствола, по документу; в данных вместимость 9. В игре станция — `neutral_fire_station` (`game/data/neutral/civilian.yaml`); модели подключены к ней в `game/data/visuals/models.yaml` со всеми состояниями.

## Открытые вопросы

1. Ворота: отдельные узлы нужны для анимации открытия, см. [appearance](appearance.md), вопрос 1.
