# 14. Автобусная остановка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Автобусная остановка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели автобусной остановки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_bus_stop` | объект | 2x1 | 2,2 | ≤ 2 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a bus stop shelter on a city pavement in neutral daylight: a metal and glass canopy roof on three posts, a bench inside, a plain route board on a post with no text. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_bus_stop`

```text
A bus stop shelter: a metal and glass canopy roof on three posts, a bench inside, and a plain route board on a post with no text, about two units long and two and a fifth units tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body` (каркас и лавка), `roof` (навес), `glass` (стеклянные панели).
- **Не блокирует проход** — навес стоит у тротуара, данные карты это учитывают.
- **Табличка маршрута без текста**: цифры и буквы не рисуем, интерфейс языка не зависит от модели.
- **Точка сбора людей** — правило жителей ([13_civilians](../../map_elements/13_civilians/prompts.md)), не модель.
- **Бюджет** ≤ 2 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_bus_stop.glb` | | | |

## Открытые вопросы

1. Будет ли в навесе свет ночью.
2. Ходят ли по улицам автобусы как гражданская техника, или остановка только для людей.
