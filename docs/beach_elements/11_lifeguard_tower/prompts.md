# 11. Вышка спасателей: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Вышка спасателей](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели вышки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `beach_lifeguard_tower` | здание | 2x2 | 3 | ≤ 5 000 | `body`, `cabin`, `roof` | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a wooden lifeguard tower on a beach in bright summer daylight: four light wooden legs, a ladder, a small glass cabin on top with a bright orange band, a flat roof. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_lifeguard_tower`

```text
A wooden lifeguard tower on four light wooden legs about three units tall, a ladder up one side, a small cabin on top with windows on all four sides and a bright orange band around it, a flat roof. No flag, no lettering. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body` (опоры и лестница), `cabin` (кабина, гарнизон), `roof`.
- Кабина — отдельный узел: в ней стоят бойцы (код), окна видны.
- Пояс цвета — не команда, а нейтральный декор. Цвета команды нет.
- Флаг не делаем.
- Бюджет ≤ 5 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_lifeguard_tower.glb` | | | |

## Открытые вопросы

1. Бонус обзора из кабины (см. [README](README.md)).
