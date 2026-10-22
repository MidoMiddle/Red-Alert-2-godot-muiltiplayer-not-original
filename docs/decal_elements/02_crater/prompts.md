# 02. Воронка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Воронка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстур воронок. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Декаль — не модель, поэтому раздел 3 требований про Meshy и Tripo здесь не нужен. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `decal_crater_small` | декаль с прозрачностью | 512² | малая воронка (граната) |
| `decal_crater_large` | декаль с прозрачностью | 512² | большая воронка (ракета, бомба) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/decals/`.

```text
Concept art reference for a shell crater on bare earth seen from directly above: a round shallow dent with a darker centre, a raised rim of loose clods of earth around it, scattered debris. Top-down view, even daylight, square image, no people, no text, no logos.
```

## 3. Текстуры декалей (генератор изображений)

### 3.1. `decal_crater_small`

```text
Small crater decal on a plain background: a round shallow dent with a dark centre, a rim of loose clods of earth, seen from directly above, square image, no text.
```

### 3.2. `decal_crater_large`

```text
Large crater decal on a plain background: a wide round dent with a dark centre, a broad rim of scattered clods and loose earth, seen from directly above, square image, no text.
```

Фон вырезаем после генерации, декаль сохраняем с прозрачностью.

## 4. Наши требования

- Декаль — текстура на плоскости, 2 треугольника. Бюджет треугольников не применяется.
- Цвет серо-коричневый: шейдер окрашивает воронку под покрытие.
- PNG 512², с альфой. Край мягкий.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `decal_crater_small.png` | | | |
| `decal_crater_large.png` | | | |

## Открытые вопросы

Открытых вопросов по текстурам нет. Вопросы по поведению — в [README](README.md).
