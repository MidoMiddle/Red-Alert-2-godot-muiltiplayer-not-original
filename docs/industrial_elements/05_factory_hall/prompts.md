# 05. Заводской цех: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Заводской цех](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `factory_hall` | здание | 4x3 | 5 | ≤ 12 000 | да, 6 | нет (нейтральный) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/industrial/`.

```text
Concept art reference for an industrial factory hall: a long one-storey brick and metal building with a gabled corrugated roof, large truck gates on one long side, a row of tall windows, a short chimney and an outside ladder to the roof, soot on the lower walls. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `factory_hall`

```text
A long industrial factory hall about four units long, three units wide and five units tall: red-brown brick walls with soot at the base, a gabled corrugated metal roof, large truck gates on one long side, a row of tall windows, a short chimney and an outside ladder to the roof. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 4x3`.

## 4. Наши требования

- Узлы: `body` (корпус), `roof`, `gate` (ворота для грузовиков), `garrison_windows` (окна, через которые стреляет гарнизон), `chimney` (короткая труба, часть модели).
- Гарнизон 6: окна видны в модели, на них опирается код.
- Текстуры 2048² (большое здание). Бюджет ≤ 12 000 треугольников.
- Цвета команды нет: здание нейтральное.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `factory_hall.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_factory_hall.py`) | 2026-10-30 | сделано, 5 474 треугольников |
| `factory_hall_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_factory_hall.py`) | 2026-10-30 | сделано, 5 794 треугольников |
| `factory_hall_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_factory_hall.py`) | 2026-10-30 | сделано, 5 004 треугольников |
| `factory_hall_damaged_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_factory_hall.py`) | 2026-10-30 | сделано, 5 326 треугольников |
| `factory_hall_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_factory_hall.py`) | 2026-10-30 | сделано, 4 124 треугольников |
| `factory_hall_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_factory_hall.py`) | 2026-10-30 | сделано, 1 098 треугольников |

## Открытые вопросы

Открытых вопросов по модели нет.
