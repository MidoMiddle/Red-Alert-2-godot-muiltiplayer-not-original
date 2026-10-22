# 14. Туннели: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Туннели](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса, портала туннеля и створки ворот. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы |
|---|---|---|---|---|
| `tunnel_portal` | 2x1 | 1,5 | ≤ 3 000 | `body` |
| `tunnel_gate` | 1x1 | 1,2 | ≤ 800 | `gate` (пивот — петля) |

Внутри туннеля ничего не видно: модели только у входа. Переход юнита и очередь — в коде.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/tunnels/`.

```text
Concept art reference for a tunnel entrance in a winter rock cliff: a concrete portal set into the rock with a dark opening, a steel grille gate half open in front of it, snow and icicles at the threshold. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `tunnel_portal`

```text
A concrete tunnel portal built into a rock cliff face: a dark rectangular opening framed in grey concrete, snow and icicles at the threshold, a blank metal plate above the opening with no text and no symbols. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`.

### 3.2. `tunnel_gate`

```text
A steel grille gate leaf with vertical bars on a single hinge, frost on the bars, one unit wide and about one and a fifth units tall. Realistic physically based materials, clean readable silhouette, game-ready asset, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Пивот ставим на петлю, узел назвать `gate`.

## 4. Наши требования

- Портал — один узел `body`. Створка — отдельный узел `gate`, иначе не открыть и не закрыть анимацией.
- Анимация открытия — поворот узла `gate` в коде. Отдельных клипов не нужно.
- Табличку над порталом оставляем пустой. Текст и знаки не используем (см. [требования](../generation_requirements.md), раздел 8).
- Цвет команды не нужен.
- Снег и сосульки — часть модели, их не выносим в эффекты.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `tunnel_portal.glb` | | | |
| `tunnel_gate.glb` | | | |

## Открытые вопросы

1. Нужны ли туннели в стартовом наборе карт или только как инструмент редактора.
2. Можно ли заблокировать вход: тогда нужна модель закрытых ворот в другом состоянии (уже есть `gate`, нужен только поворот).
