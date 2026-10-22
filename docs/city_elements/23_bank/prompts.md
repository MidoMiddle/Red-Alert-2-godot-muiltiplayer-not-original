# 23. Банк: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Банк](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели банка. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_bank` | здание | 3x2 | 5 | ≤ 10 000 | да, 4 | да (полоса над фронтоном) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small stone bank building on a city square in neutral daylight: a two-storey grey stone building with white columns at the entrance, a triangular pediment above the door, tall windows with dark frames, steps leading up to the door. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_bank`

```text
A small stone bank building about three units wide, two units deep and five units tall: a grey stone facade with white columns at the entrance, a triangular pediment above the door, tall windows with dark frames, steps to the door, a narrow colour band above the pediment. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `columns` (колонны), `pediment` (фронтон), `steps` (ступени), `garrison_windows` (окна, через которые стреляет гарнизон), `team_mask` (полоса над фронтоном).
- **Гарнизон 4:** окна видны в модели, на них опирается код.
- **Бюджет** ≤ 10 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_bank.glb` | | | |

## Открытые вопросы

1. Подсветка окон ночью: см. [appearance](appearance.md), вопрос 1.
