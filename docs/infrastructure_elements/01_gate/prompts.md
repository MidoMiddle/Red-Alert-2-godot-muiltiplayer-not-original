# 01. Ворота: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Ворота](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `gate` | средний объект | 1x1 | 0,9 (столбы), створки 0,62 | ≤ 3 000 | нет | да (полоса вокруг верха столбов) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/infrastructure/`.

```text
Concept art reference for a wall gate section in an industrial base fence: two concrete posts with a gate of two metal leaves between them, the leaves closed in the middle, a sliding latch bar, grey metal mesh and dark green paint, rust at the hinges. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `gate`

```text
A wall gate section one unit wide and about one unit tall: two concrete posts and between them a pair of closed metal gate leaves in grey mesh and dark green paint, a sliding latch bar in the middle, rust at the hinges, a narrow colour band round the top of each post. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Узлы: `post_left`, `post_right` (столбы), `leaf_left`, `leaf_right` (створки; пивот на петле, чтобы анимация открытия работала), `latch` (засов), `team_mask` (полоса вокруг верха столбов), `base` (фундаменты и порог). У разбитых — `body` и `base`.
- Пивот в центре клетки на земле. Размер игры, без сжатия: столбы до 0,9, створки 0,62. Столбы на концах клетки, между ними 0,84.
- Текстуры 1024². Бюджет ≤ 3 000 треугольников.
- Цвет команды через `team_mask` (R-канал или вершинный цвет, M1 4.6). Нейтральные ворота полосу не получают.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `gate.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/infrastructure_gate.py`) | 2026-10-31 | сделано, 1 222 треугольника |
| `gate_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/infrastructure_gate.py`) | 2026-10-31 | сделано, 1 150 треугольников |
| `gate_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/infrastructure_gate.py`) | 2026-10-31 | сделано, 1 330 треугольников |

## Открытые вопросы

Открытых вопросов по модели нет. Вопрос о полосе цвета — в [appearance](appearance.md).
