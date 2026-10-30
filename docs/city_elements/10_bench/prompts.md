# 10. Скамейка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Скамейка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели скамейки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_bench` | объект | 1x1 | 0,5 | ≤ 500 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a park bench on a city pavement in neutral daylight: wooden slats on a dark green cast-iron frame, armrests and a backrest. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_bench`

```text
A park bench one unit long: wooden slats on a dark green cast-iron frame, with armrests and a backrest. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле.
- **Не блокирует проход** — это данные карты, в модели не отражается.
- **Сидящих людей в модели нет.** Позы сидящих жителей — отдельная задача ([13_civilians](../../map_elements/13_civilians/prompts.md)), если решим их делать.
- **Бюджет** ≤ 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_bench.glb` | Blender, скрипт `tools/blender/families/city_bench.py` | 2026-10-29 | готово |
| `city_bench_damaged.glb` | то же | 2026-10-29 | готово |
| `city_bench_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Три состояния по [appearance.md](appearance.md): целая, повреждённая (сломаны планки, спинки нет) и разрушенная (боковины опрокинуты, доски на земле). Узел `bench`. Высота не 0,5, а 0,42 до верха спинки, в размере, в котором скамейку рисует игра: объекты карты не сжимаются по высоте. Сидящих людей нет. В игре скамейка — объект карты `bench` (`game/data/map/objects.yaml`); целая модель подключена к нему в `game/data/visuals/models.yaml`. Состояния объектов карты клиент пока не показывает.

## Открытые вопросы

1. Ставить ли скамейки только в сквере или и на тротуарах.
2. Сидят ли на скамейках жители (визуальные позы).
