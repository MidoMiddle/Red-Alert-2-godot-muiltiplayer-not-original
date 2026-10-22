# 07. Киоск: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Киоск](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели киоска. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_kiosk` | здание | 1x1 | 1,5 | ≤ 2 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small street kiosk on a city corner in neutral daylight: a single-cell wooden pavilion with a serving hatch and a metal grille, a dark canopy with a bright stripe, a small plain sign board without text. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_kiosk`

```text
A small kiosk pavilion on a one unit square base: dark green wooden cladding, a serving hatch with a metal grille, a dark canopy with a bright red stripe, and a small plain sign board without text. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `body` (павильон), `canopy` (навес, горит первым).
- **Гарнизона нет**: павильон слишком мал для бойцов.
- **Окошко** с решёткой — геометрия, на него смотрят жители.
- **Горение навеса** — код и эффекты. В модели навес обычный.
- **Бюджет** ≤ 2 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_kiosk.glb` | | | |

## Открытые вопросы

1. Показывать ли продавца в окошке (отдельная модель человека, по поведению — из [13_civilians](../../map_elements/13_civilians/prompts.md)).
