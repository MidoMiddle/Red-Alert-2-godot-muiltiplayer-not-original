# 04. Морская нефтяная платформа: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Морская нефтяная платформа](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели платформы. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `offshore_platform` | здание на воде | 2x2 | 2,1 (вышка), палуба 0,6 | ≤ 8 000 | `body`, `derrick`, `beam`, `team_mask`, `flare_anchor` | да (верхний поручень перил) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for an offshore oil platform in bright summer daylight: a square white deck on four thick legs standing in the sea, a lattice derrick and a pump on the deck, a dark rusty base in the water. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `offshore_platform`

```text
An offshore oil platform about four units tall: a square white deck on four thick legs standing in the sea, a lattice derrick and a small pump on the deck, a narrow plain colour band on the deck railing, rust at the waterline. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`. Пивот — центр футпринта на уровне воды.

## 4. Наши требования

- Узлы: `body` (палуба, ноги и всё на палубе), `derrick` (вышка), `beam` (качалка насоса, пивот на оси — качание код), `team_mask` (верхний поручень), `flare_anchor` (конец факельной стрелы, для пламени). Вертолётная площадка — часть `body` у целой платформы. У покинутой — ещё `fire_1..3`, у руин только `body`.
- Размер игры, как у нефтевышки техзданий: палуба 0,6, вышка до 2,1. Пивот — центр футпринта на поверхности воды, ноги уходят под воду до −0,15.
- Блокирует флот и амфибий. Пламя и дым — частицы.
- Текстуры 2048² (большое техздание), бюджет ≤ 8 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `offshore_platform.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_platform.py`) | 2026-11-01 | сделано, 3 962 треугольника (с вертолётной площадкой) |
| `offshore_platform_abandoned.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_platform.py`) | 2026-11-01 | сделано, 3 698 треугольников |
| `offshore_platform_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_platform.py`) | 2026-11-01 | сделано, 1 752 треугольника |

## Открытые вопросы

Обломанная платформа сделана отдельной моделью — `offshore_platform_ruins.glb` (2026-11-01).
