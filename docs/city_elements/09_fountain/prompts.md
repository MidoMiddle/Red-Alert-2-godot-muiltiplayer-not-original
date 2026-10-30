# 09. Фонтан: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Фонтан](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели фонтана. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_fountain` | объект | 2x2 | 0,8 | ≤ 3 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a round stone fountain in the centre of a city square in neutral daylight: a wide grey stone basin with a low rim, a central column with a water jet, a darker water line inside the basin. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_fountain`

```text
A round stone fountain with a wide grey basin, a low rim, and a central column base, with a darker water line inside the basin. No water and no jet in the model. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узел: `basin` (чаша), остальное — в одной модели.
- **Струи и вода** — частицы и шейдер в Godot, в модели их нет. Вода не меняет тип клетки ([02_land_types](../../map_elements/02_land_types/prompts.md)): под фонтаном остаётся обычная земля.
- **Прочность высокая**: разрушается только тяжёлым оружием. Это данные, модель одна.
- **Гарнизона нет.** Цвета команды нет.
- **Бюджет** ≤ 3 000 треугольников (средний объект), текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_fountain.glb` | Blender, скрипт `tools/blender/families/city_fountain.py` | 2026-10-29 | готово |
| `city_fountain_damaged.glb` | то же | 2026-10-29 | готово |
| `city_fountain_abandoned.glb` | то же | 2026-10-29 | готово |
| `city_fountain_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Четыре состояния по [appearance.md](appearance.md): целый, повреждённый (трещины, вода ниже), сильно повреждённый (бортик пробит, колонна упала, вода вытекла на площадь) и разрушенный (пустая чаша с обломками). Высота не 0,8, а 0,66 до верха колонны, в размере, в котором фонтан рисует игра: его не сжимают по высоте, как дома. Струй нет, но вода в модели есть: плоская поверхность в чаше и в верхней чаше, отдельным узлом `water`. Без неё чаша выглядит пустой, а шейдера воды для фонтана пока нет; клиент может скрыть узел или дать ему свой шейдер. В игре фонтан — `neutral_fountain` (`game/data/neutral/civilian.yaml`); модели подключены к нему в `game/data/visuals/models.yaml`.

## Открытые вопросы

1. Делать ли фонтан неразрушаемым, как ориентир на площади.
2. Подсветка фонтана ночью: визуальная опция, решим в M11.
