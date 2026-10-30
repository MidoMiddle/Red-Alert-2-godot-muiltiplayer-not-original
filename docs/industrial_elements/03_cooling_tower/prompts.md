# 03. Градирня: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Градирня](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `cooling_tower` | здание | 2x2 | 5 | ≤ 12 000 | нет | нет (нейтральный) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/industrial/`.

```text
Concept art reference for a concrete industrial cooling tower: a wide hyperboloid tower, narrower at the top, with an open top and a white plume of steam rising from it, light grey weathered concrete with dark streaks, a low concrete base with a rail. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `cooling_tower`

```text
A large concrete cooling tower about two units wide and five units tall, a hyperboloid shape wider at the bottom and narrower at the top with an open top, light grey weathered concrete with dark streaks, a low concrete base. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Узлы: `body` (башня), `base` (плита), `steam_anchor` (точка выхода пара для эффекта).
- Пивот в центре основания на земле, высота 5.
- Текстуры 2048² (большое здание). Бюджет ≤ 12 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `cooling_tower.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_cooling_tower.py`) | 2026-10-30 | сделано, 3 836 треугольников |
| `cooling_tower_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_cooling_tower.py`) | 2026-10-30 | сделано, 3 764 треугольников |
| `cooling_tower_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_cooling_tower.py`) | 2026-10-30 | сделано, 3 932 треугольников |
| `cooling_tower_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/industrial_cooling_tower.py`) | 2026-10-30 | сделано, 2 218 треугольников |

## Открытые вопросы

Открытых вопросов по модели нет.
