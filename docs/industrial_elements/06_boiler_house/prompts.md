# 06. Котельная: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Котельная](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `boiler_house` | здание | 2x2 | 3,5 | ≤ 8 000 | да, 2 | нет (нейтральный) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/industrial/`.

```text
Concept art reference for a small industrial boiler house: a low square brick building with a flat tar roof, a short metal chimney on the roof, narrow barred windows in two rows, a steel door, soot near the chimney. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `boiler_house`

```text
A small industrial brick boiler house about two units wide and two units long and three and a half units tall: dark red brick walls, a flat tar roof, a short metal chimney on the roof, narrow barred windows in two rows, a steel door. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Узлы: `body` (стены), `roof`, `chimney` (короткая труба), `door`, `garrison_windows` (окна, через которые стреляет гарнизон).
- Гарнизон 2: окна видны в модели, на них опирается код.
- Текстуры 1024² (небольшое здание). Бюджет ≤ 8 000 треугольников.
- Цвета команды нет.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `boiler_house.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_boiler_house.py`) | 2026-10-30 | сделано, 3 426 треугольников |
| `boiler_house_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_boiler_house.py`) | 2026-10-30 | сделано, 3 622 треугольников |
| `boiler_house_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_boiler_house.py`) | 2026-10-30 | сделано, 3 378 треугольников |
| `boiler_house_damaged_occupied.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_boiler_house.py`) | 2026-10-30 | сделано, 3 574 треугольников |
| `boiler_house_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_boiler_house.py`) | 2026-10-30 | сделано, 3 126 треугольников |
| `boiler_house_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_boiler_house.py`) | 2026-10-30 | сделано, 1 366 треугольников |

## Открытые вопросы

Открытых вопросов по модели нет.
