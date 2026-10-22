# 06. Контейнерные штабеля: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Контейнерные штабеля](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели штабеля контейнеров. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `container_stack` | объект | 2x1 | 2,5 | ≤ 3 000 | `box_bottom`, `box_top` | наземных |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for stacked steel shipping containers on a harbour quay in bright summer daylight: two rows of ribbed red and blue containers stacked two high, no writing, dents and rust on the corners. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `container_stack`

```text
A stack of two steel shipping containers about two units long and two and a half units tall: ribbed walls, a red container at the bottom and a blue one on top, dents and rust on the corners, no writing and no logos. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`. Узлы: `box_bottom`, `box_top`.

## 4. Наши требования

- Расцветки — варианты текстуры (красный, синий, зелёный, серый). Одна модель.
- Надписи и логотипы не делаем.
- Блокирует наземных. Цвета команды нет.
- Бюджет ≤ 3 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `container_stack.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
