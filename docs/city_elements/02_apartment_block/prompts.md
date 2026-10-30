# 02. Многоэтажный жилой дом: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Многоэтажный жилой дом](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели многоэтажки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

Это не тот же ассет, что `building_apartment_block` в [11_civilian_buildings](../../map_elements/11_civilian_buildings/prompts.md), там здание под снегом. Здесь отдельная модель `city_apartment_block`.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_apartment_block` | здание | 3x3 | 6 | ≤ 15 000 | да, 8 | да (полоса под карнизом) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a five-storey apartment block on a city street in neutral daylight: a flat roof, rows of identical windows, a central entrance with a canopy, small balconies, and a narrow plain band under the cornice. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_apartment_block`

```text
A five-storey apartment block with a flat roof, rows of identical windows, a central entrance with a canopy, small balconies with plain railings, and a narrow plain colour band under the cornice. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `team_mask` (полоса под карнизом).
- **Гарнизон 8** (максимум по M11 — 10). Бойцы в окнах и полоса цвета владельца — поведение и маска.
- **Балконы** — геометрия, не текстура: по ним видно этажность с расстояния.
- **Повреждённый вариант** (`city_apartment_block_damaged`) — тот же промт, что `building_apartment_block_damaged` в [11_civilian_buildings](../../map_elements/11_civilian_buildings/prompts.md), без снега.
- **Бюджет** ≤ 15 000 треугольников, текстуры 2048² (большое здание).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_apartment_block.glb` | Blender, скрипт `tools/blender/families/city_apartment.py` | 2026-10-29 | готово |
| `city_apartment_block_occupied.glb` | то же | 2026-10-29 | готово |
| `city_apartment_block_damaged.glb` | то же | 2026-10-29 | готово |
| `city_apartment_block_damaged_occupied.glb` | то же | 2026-10-29 | готово |
| `city_apartment_block_abandoned.glb` | то же | 2026-10-29 | готово |
| `city_apartment_block_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Цвет владельца — весь карниз по верху здания: он выступает из стен и чуть поднимается над крышей, поэтому виден издалека и после сжатия по высоте. Файлы лежат в `game/assets/models/city/`. В `models.yaml` модели не подключены: городской многоэтажки нет в данных игры (`neutral_apartment` рисуется зимней).

## Открытые вопросы

1. Вместимость 8 или ближе к максимуму 10. Решаем на балансе.
