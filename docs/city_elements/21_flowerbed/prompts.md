# 21. Клумба: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Клумба](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели клумбы. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_flowerbed` | объект | 1x1 | 0,4 | ≤ 500 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small city flowerbed: a low rectangular raised bed with a grey stone edge, dark brown soil and a mix of red and yellow flowers with low green shrubs. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_flowerbed`

```text
A low rectangular flowerbed about one unit long and 0.4 units tall: a grey stone edge around dark brown soil, low green shrubs and a few red and yellow flowers. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `edge` (бордюр), `soil` (земля), `plants` (цветы и кусты).
- **Не блокирует проход** — это данные карты, в модели не отражается.
- **Бюджет** ≤ 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_flowerbed.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
