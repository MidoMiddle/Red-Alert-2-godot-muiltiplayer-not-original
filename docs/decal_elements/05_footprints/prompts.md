# 05. Следы пехоты: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Следы пехоты](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры следов пехоты. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Декаль — не модель, поэтому раздел 3 требований про Meshy и Tripo здесь не нужен. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `decal_footprints` | декаль с прозрачностью | 512² | цепочка следов ног |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/decals/`.

```text
Concept art reference for a trail of boot footprints in fresh snow seen from directly above: alternating left and right prints in a line, soft blue-grey shadows inside each print. Top-down view, even daylight, square image, no people, no text, no logos.
```

## 3. Текстура декали (генератор изображений)

### 3.1. `decal_footprints`

```text
Footprint trail decal on a plain background: a line of alternating boot footprints in dark grey with soft edges, seen from directly above, square image, no text.
```

Фон вырезаем после генерации, декаль сохраняем с прозрачностью.

## 4. Наши требования

- Декаль — текстура на плоскости, 2 треугольника. Бюджет треугольников не применяется.
- В одной декали два-три отпечатка. Шаг по цепочке делает шейдер или код, не текстура.
- PNG 512², с альфой.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `decal_footprints.png` | | | |

## Открытые вопросы

Открытых вопросов по текстуре нет. Вопросы по поведению — в [README](README.md).
