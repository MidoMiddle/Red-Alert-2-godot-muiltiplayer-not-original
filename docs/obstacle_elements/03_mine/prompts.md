# 03. Мина: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Мина](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса, модели мины и иконки метки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль нейтральный, без климата и без национальных символов. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Видна в игре |
|---|---|---|---|---|---|
| `mine_pressure` | объект | 1x1 | 0,15 | ≤ 300 | только после обнаружения |
| `icon_mine_marker` | иконка с прозрачностью, 2D | 256² | — | — | метка в редакторе и на карте |

Модель нужна, когда мина обнаружена или видна владельцу. Пока она скрыта, модель не рисуется.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/obstacles/`.

```text
Concept art reference for a buried anti-vehicle mine in the ground: a low round metal disc with a raised pressure cap, flush with the soil, a few loose soil grains around the edge. Top-down view close to 45 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no national symbols.
```

## 3. Модель и иконка

### 3.1. `mine_pressure` (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

```text
A low round buried mine: a thin flat metal disc about half a unit across with a raised pressure cap on top, dark olive steel, a few soil grains around the edge, no text, no national symbols. Realistic physically based materials, clean readable silhouette, game-ready asset, neutral daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

### 3.2. `icon_mine_marker` (генератор изображений)

```text
Flat white warning diamond icon with a bold dark exclamation mark, monochrome, simple, plain background, square image, no text except the symbol.
```

Фон вырезаем после генерации. Иконка — 2D, в 3D-модель не превращается. Цвет задаёт код: красный — обнаружена, цвет команды — своя мина.

## 4. Наши требования

- Пивот — центр клетки на земле, низ диска на уровне земли. Узлы: `body` (диск), `cap` (нажимная крышка).
- Модель не блокирует проход: это данные карты.
- Иконка — отдельный файл, не текстура модели: в редакторе она нужна и без модели.
- Цвет метки задаёт шейдер: красный (обнаружена), цвет команды (своя мина).
- Цвета команды нет.
- Бюджет ≤ 300 треугольников, текстуры 256².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `mine_pressure.glb` | | | |
| `icon_mine_marker.png` | | | |

## Открытые вопросы

1. Стоимость и время установки, лимит мин (см. [README](README.md)).
