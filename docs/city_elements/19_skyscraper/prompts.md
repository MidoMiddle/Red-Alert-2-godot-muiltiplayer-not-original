# 19. Небоскрёб: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Небоскрёб](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели небоскрёба. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_skyscraper` | здание | 2x2 | 9 | ≤ 12 000 | да, 10 | да (полоса по краю крыши) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a slender glass-and-steel skyscraper in a city centre in neutral daylight: vertical strips of dark blue glass running from the ground floor to the roof, a plain glass entrance hall, a short antenna mast on the flat roof. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_skyscraper`

```text
A slender glass-and-steel skyscraper about two units wide and nine units tall: vertical strips of dark blue glass running from the ground floor to the roof, a glass ground floor with a plain sign board without text, a flat roof with a short antenna mast and a narrow colour band along the roof edge. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `antenna`, `team_mask` (полоса по краю крыши).
- **Высота 9** — выше офиса (6). Так решено в [README](README.md), раздел 4.
- **Стекло** — отдельный материал (`glass`), как у офиса ([04](../04_office/prompts.md)).
- **Гарнизон 10.** Бойцы в верхних окнах — код.
- **Бюджет** ≤ 12 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_skyscraper.glb` | | | |

## Открытые вопросы

1. Сколько окон видно для бойцов. От этого зависит вместимость 10 (см. [README](README.md), вопрос 1).
