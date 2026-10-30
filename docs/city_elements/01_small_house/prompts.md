# 01. Малый жилой дом: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Малый жилой дом](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели дома. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

Это не тот же ассет, что дом на зимней карте (`building_house_small` в [11_civilian_buildings](../../map_elements/11_civilian_buildings/prompts.md)): там дом под снегом. Здесь отдельная модель `city_house_small`.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_house_small` | здание | 2x2 | 2,5 | ≤ 8 000 | да, 3 | да (полоса над дверью) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small two-storey brick house on a city street in neutral daylight: a pitched dark tiled roof, white window frames with curtains, a small wooden porch with a canopy, a low fence around a small front garden. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_house_small`

```text
A small two-storey brick house with a pitched dark tiled roof, white window frames with curtains, a wooden porch with a small canopy, a low fence around a small front garden, and a narrow plain colour band on the wall above the door. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `team_mask` (полоса над дверью).
- **Гарнизон 3.** Силуэты бойцов в окнах и полоса цвета владельца — поведение и маска, в модели только окна и полоса.
- **Повреждённый вариант** (`city_house_small_damaged`) — тот же промт, что `building_house_small_damaged` в [11_civilian_buildings](../../map_elements/11_civilian_buildings/prompts.md), без снега.
- **Окна** должны быть видны: по ним игрок понимает, что дом можно занять (см. [appearance.md](appearance.md)).
- **Бюджет** ≤ 8 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_house_small.glb` | Blender, скрипт `tools/blender/families/city_house.py` | 2026-10-29 | готово |
| `city_house_small_occupied.glb` | то же | 2026-10-29 | готово |
| `city_house_small_damaged.glb` | то же | 2026-10-29 | готово |
| `city_house_small_damaged_occupied.glb` | то же | 2026-10-29 | готово |
| `city_house_small_abandoned.glb` | то же | 2026-10-29 | готово |
| `city_house_small_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Цвет владельца у неё на козырьке над дверью, а не полосой на стене: игра сжимает дом по высоте, и узкая полоса перестаёт читаться. Файлы лежат в `game/assets/models/city/`. В `models.yaml` подключены ключом `building/neutral_house_a@urban` со всеми состояниями: на городских картах `neutral_house_a` рисуется этим домом, на остальных — прежним.

## Открытые вопросы

1. Нужны ли два–три варианта фасада, чтобы улица не выглядела копией.
