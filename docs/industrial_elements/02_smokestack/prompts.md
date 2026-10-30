# 02. Дымовая труба: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Дымовая труба](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `smokestack` | средний объект | 1x1 | 6 | ≤ 3 000 | нет | нет (нейтральный) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/industrial/`.

```text
Concept art reference for a tall brick industrial smokestack: a tapering red-brown brick chimney on a grey concrete base, a dark heat-resistant ring at the top, dark soot at the bottom, a thin column of grey smoke rising from the top. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `smokestack`

```text
A tall industrial brick smokestack about one unit wide at the base and six units tall, tapering towards the top: red-brown brick with dark mortar joints, a grey concrete base, a dark ring at the top. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Узлы: `body` (труба), `base` (фундамент), `smoke_anchor` (точка выхода дыма для эффекта).
- Пивот в центре клетки на земле, высота 6.
- Текстуры 1024². Бюджет ≤ 3 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `smokestack.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_smokestack.py`) | 2026-10-30 | сделано, 2 670 треугольников |
| `smokestack_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_smokestack.py`) | 2026-10-30 | сделано, 2 862 треугольников |
| `smokestack_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_smokestack.py`) | 2026-10-30 | сделано, 1 842 треугольников |
| `smokestack_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_smokestack.py`) | 2026-10-30 | сделано, 1 038 треугольников |

## Открытые вопросы

1. Нужна ли отдельная модель с падением, где обломки — отдельные объекты, или хватит анимации (см. [appearance](appearance.md)).
