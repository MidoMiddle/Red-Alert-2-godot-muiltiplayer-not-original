# 22. Кафе: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Кафе](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели кафе. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_cafe` | здание | 2x1 | 3 | ≤ 6 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small city cafe in neutral daylight: a one-storey light brick building with wide shop windows, a striped awning over the entrance, and an open wooden terrace in front with three small tables and parasols. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_cafe`

```text
A small one-storey cafe about two units wide, one unit deep and three units tall: light brick walls, wide shop windows, a striped awning over the entrance, and a wooden terrace in front with three small tables and parasols. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x1`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `awning` (навес), `terrace` (веранда со столиками), `windows` (витрины).
- **Гарнизона нет.** Окна декоративные, комнат нет.
- **Бюджет** ≤ 6 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_cafe.glb` | Blender, скрипт `tools/blender/families/city_cafe.py` | 2026-10-30 | готово |
| `city_cafe_damaged.glb` | то же | 2026-10-30 | готово |
| `city_cafe_abandoned.glb` | то же | 2026-10-30 | готово |
| `city_cafe_ruins.glb` | то же | 2026-10-30 | готово |

Модель сделана скриптом, без генератора. Гарнизона нет, поэтому состояний четыре по [appearance.md](appearance.md): целое, повреждённое (витрина разбита, навес порван и висит, столики перевёрнуты), сгоревшее (файл `abandoned`: витрины выбиты и в копоти, от навеса осталась рама, веранда обуглена, столики и зонтики повалены) и разрушенное (обрубки стен, обломки, обгоревшие доски и столик на тротуаре). Узлы `body`, `windows`, `awning`, `terrace`, `base`. Столики с зонтиками — часть модели (открытый вопрос 1 решён так). Высота: этаж до крыши 1,5, парапет 1,68, после сжатия игрой ниже магазина. В игре кафе — `neutral_cafe` (`game/data/neutral/civilian.yaml`); модели подключены к нему в `game/data/visuals/models.yaml`.

## Открытые вопросы

1. Столики на веранде: часть модели или отдельные объекты, которые ставит карта.
