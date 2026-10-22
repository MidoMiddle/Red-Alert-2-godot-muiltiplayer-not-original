# 03. Школа: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Школа](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели школы. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_school` | здание | 3x2 | 3 | ≤ 10 000 | да, 6 | да (полоса над входом) |

Двор и ограда — не в этой модели: секции забора делаем по [08_fences_walls](../../map_elements/08_fences_walls/prompts.md).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a two-storey school building in a city block in neutral daylight: a long facade with rows of classroom windows on both floors, a wide entrance porch with a canopy, a clock above the entrance, and a small fenced yard with asphalt. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no flagpole.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_school`

```text
A long two-storey school building with a row of classroom windows on both floors, a wide entrance porch with a canopy, a clock on the facade above the entrance, and a narrow plain colour band above the entrance. No flagpole, no lettering. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `team_mask` (полоса над входом).
- **Гарнизон 6.** Бойцы в окнах второго этажа и на крыльце — код, в модели окна видны.
- **Флагштока нет** в модели: это открытый вопрос из [appearance.md](appearance.md). Если решим добавить, это отдельный узел `flagpole`, а полотно флага — в цвете команды.
- **Без надписей** на фасаде и на часах: только цифры или ничего.
- **Повреждённый вариант** — по промту `building_house_small_damaged` из [11_civilian_buildings](../../map_elements/11_civilian_buildings/prompts.md) с правкой под школу. Отдельный промт не пишем, пока не решено, нужен ли он.
- **Бюджет** ≤ 10 000 треугольников, текстуры 2048² (крупное здание).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_school.glb` | | | |

## Открытые вопросы

1. Оставить ли школе гарнизон или сделать её декором для атмосферы.
2. Нужен ли флагшток во дворе, и будет ли флаг в цвете владельца.
