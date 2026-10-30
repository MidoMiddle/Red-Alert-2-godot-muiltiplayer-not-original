# 01. Топливный резервуар: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Топливный резервуар](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `fuel_tank` | средний объект | 1x1 | 2,5 | ≤ 3 000 | нет | нет (нейтральный) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/industrial/`.

```text
Concept art reference for a steel fuel tank on short legs at an industrial site: a grey-blue cylindrical tank with ribbed bands, a side ladder to a hatch, a rail on top, a yellow triangular hazard sign with a black flame symbol. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `fuel_tank`

```text
A steel fuel tank about one unit across and two and a half units tall, standing on four short legs: a grey-blue cylindrical tank with ribbed bands, a side ladder to a hatch, a small rail on top, a yellow triangular hazard sign on the side. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Узлы: `body` (корпус), `legs` (опоры), `ladder`, `hazard_sign` (знак, отдельный узел — цвет и значок меняются), `rail`.
- Пивот в центре клетки на земле, высота 2,5.
- Текстуры 1024². Бюджет ≤ 3 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `fuel_tank.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_fuel_tank.py`) | 2026-10-30 | сделано, 2 398 треугольников |
| `fuel_tank_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_fuel_tank.py`) | 2026-10-30 | сделано, 2 514 треугольников |
| `fuel_tank_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_fuel_tank.py`) | 2026-10-30 | сделано, 2 522 треугольников |
| `fuel_tank_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_fuel_tank.py`) | 2026-10-30 | сделано, 782 треугольников |

## Открытые вопросы

1. Цвет знака: жёлтый или красный, как у бочки (см. [appearance](appearance.md)).
