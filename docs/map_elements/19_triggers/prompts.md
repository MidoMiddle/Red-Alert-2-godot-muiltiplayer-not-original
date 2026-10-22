# 19. Триггеры и сценарные объекты: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Триггеры и сценарные объекты](README.md) · [Требования к генерации](../generation_requirements.md)

Триггеры в игре невидимы. Видны только маркеры задач и стрелки, и это 2D-иконки. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `icon_objective_marker` | иконка с прозрачностью | 256² | маркер задачи над целью и на мини-карте |
| `icon_direction_arrow` | иконка с прозрачностью | 256² | стрелка к цели за краем экрана (поворот в коде) |
| `icon_danger_zone` | иконка с прозрачностью, по желанию | 256² | опасная зона (ловушка) |

Моделей нет. Триггер и его область — данные карты (M7, раздел «Формат карты»), в игре видны только эти маркеры.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/triggers/`.

```text
Concept art reference for mission markers in a real-time strategy game: a bright yellow map pin with a bold dark outline and a small inner circle, a white chevron arrow, and a red warning triangle. Flat icon style, readable at small size, plain background, no text.
```

## 3. Иконки (генератор изображений)

### 3.1. `icon_objective_marker`

```text
A flat icon of a bright yellow map pin with a bold dark outline and a small inner circle, simple, readable at small size, plain background, no text, square image.
```

### 3.2. `icon_direction_arrow`

```text
A flat white chevron arrow icon pointing up, with a dark outline, simple, plain background, no text, square image.
```

Поворот по направлению к цели делает код.

### 3.3. `icon_danger_zone` (по желанию)

```text
A flat red warning triangle icon with a bold exclamation mark, simple, plain background, no text except the symbol, square image.
```

## 4. Наши требования

- Иконки 256² с прозрачностью. Фон вырезаем после генерации.
- Цвета: жёлтый — задача, белый — направление, красный — опасность. Они совпадают с цветами в редакторе триггеров (см. [appearance.md](appearance.md)).
- Пульсация маркера — анимация масштаба в коде, не отдельные кадры.
- Цвет команды не нужен: задачи общие для всех игроков.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `icon_objective_marker.png` | | | |
| `icon_direction_arrow.png` | | | |
| `icon_danger_zone.png` (по желанию) | | | |

## Открытые вопросы

1. Нужен ли отдельный маркер «зона опасности» для ловушек (сейчас — по желанию).
