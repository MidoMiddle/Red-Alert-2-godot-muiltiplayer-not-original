# 26. Автомастерская: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Автомастерская](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели автомастерской. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_auto_repair` | здание | 2x2 | 3 | ≤ 6 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a small city car repair garage in neutral daylight: a low grey block building with two wide roll-up bay doors, a plain sign board above them without text, a flat roof with a vent. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_auto_repair`

```text
A low city car repair garage about two units wide, two units deep and three units tall: grey block walls, two wide roll-up bay doors, a plain sign board without text above the doors, a flat roof with a vent. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `bay_doors` (створки ворот боксов, отдельные узлы для открытия), `sign` (вывеска без текста).
- **Гарнизона нет.** Проёмы боксов код не использует.
- **Бюджет** ≤ 6 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_auto_repair.glb` | | | |

## Открытые вопросы

1. Машины у входа: в модели или отдельными объектами, см. [appearance](appearance.md), вопрос 1.
