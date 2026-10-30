# 24. Гостиница: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Гостиница](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели гостиницы. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_hotel` | здание | 3x2 | 8 | ≤ 12 000 | да, 6 | да (полоса над входом) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a city hotel building in neutral daylight: a multi-storey light brick building with a narrow balcony on every floor, a dark red awning over the entrance, a plain sign board above the door, dark-framed windows in rows. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_hotel`

```text
A multi-storey city hotel about three units wide, two units deep and eight units tall: light brick facade with a narrow balcony on every floor, grey concrete balcony slabs with metal railings, a dark red awning over the entrance, a plain sign board without text, a narrow colour band above the entrance. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `balconies` (балконы), `awning` (навес), `sign` (вывеска без текста), `garrison_windows` (окна, через которые стреляет гарнизон), `team_mask` (полоса над входом).
- **Гарнизон 6:** окна видны в модели, на них опирается код.
- **Бюджет** ≤ 12 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_hotel.glb` | Blender, скрипт `tools/blender/families/city_hotel.py` | 2026-10-30 | готово |
| `city_hotel_occupied.glb` | то же | 2026-10-30 | готово |
| `city_hotel_damaged.glb` | то же | 2026-10-30 | готово |
| `city_hotel_damaged_occupied.glb` | то же | 2026-10-30 | готово |
| `city_hotel_abandoned.glb` | то же | 2026-10-30 | готово |
| `city_hotel_ruins.glb` | то же | 2026-10-30 | готово |

Модель сделана скриптом, без генератора. Высокий первый этаж и восемь этажей до крыши 7,95, с парапетом 8,15. Узкий балкон вдоль фасада и задней стены на каждом этаже (открытый вопрос 1: балконы сплошные, окна над ними все видны). Узлы `body`, `garrison_windows` (окна верхних этажей), `balconies`, `awning`, `sign`, `team_mask`, `base`. Цвет владельца — парапет по краю крыши, как у офиса и небоскрёба, а не полоса над входом: игра сжимает здание по высоте, и узкая полоса перестаёт читаться. Гарнизон: шесть стволов, по документу; в данных вместимость 9. В игре гостиница — `neutral_hotel` (`game/data/neutral/civilian.yaml`); модели подключены к ней в `game/data/visuals/models.yaml` со всеми состояниями.

## Открытые вопросы

1. Количество балконов в модели: от него зависит, сколько окон видно для бойцов. См. [README](README.md), вопрос 1.
