# 03. Копоть и выжженная земля: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Копоть и выжженная земля](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры копоти. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Декаль — не модель, поэтому раздел 3 требований про Meshy и Tripo здесь не нужен. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `decal_scorch` | декаль с прозрачностью | 512² | копоть и выжженная земля |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/decals/`.

```text
Concept art reference for a scorch mark on bare earth seen from directly above: an irregular dark soot patch with burnt, lighter edges and a few charred tufts of grass around it. Top-down view, even daylight, square image, no people, no text, no logos.
```

## 3. Текстура декали (генератор изображений)

### 3.1. `decal_scorch`

```text
Scorch mark decal on a plain background: an irregular dark soot patch, almost black in the centre with burnt grey-brown edges, seen from directly above, square image, no text.
```

Фон вырезаем после генерации, декаль сохраняем с прозрачностью.

## 4. Наши требования

- Декаль — текстура на плоскости, 2 треугольника. Бюджет треугольников не применяется.
- Цвет серо-коричневый: шейдер окрашивает копоть под покрытие.
- PNG 512², с альфой. Край размытый, без жёсткой обрезки.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `decal_scorch.png` | | | |

## Открытые вопросы

Открытых вопросов по текстуре нет. Вопросы по поведению — в [README](README.md).
