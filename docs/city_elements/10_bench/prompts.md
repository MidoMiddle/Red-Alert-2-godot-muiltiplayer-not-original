# 10. Скамейка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Скамейка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели скамейки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_bench` | объект | 1x1 | 0,5 | ≤ 500 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a park bench on a city pavement in neutral daylight: wooden slats on a dark green cast-iron frame, armrests and a backrest. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_bench`

```text
A park bench one unit long: wooden slats on a dark green cast-iron frame, with armrests and a backrest. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле.
- **Не блокирует проход** — это данные карты, в модели не отражается.
- **Сидящих людей в модели нет.** Позы сидящих жителей — отдельная задача ([13_civilians](../../map_elements/13_civilians/prompts.md)), если решим их делать.
- **Бюджет** ≤ 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_bench.glb` | | | |

## Открытые вопросы

1. Ставить ли скамейки только в сквере или и на тротуарах.
2. Сидят ли на скамейках жители (визуальные позы).
