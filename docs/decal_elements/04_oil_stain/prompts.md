# 04. Масляное пятно: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Масляное пятно](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры масляного пятна. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Декаль — не модель, поэтому раздел 3 требований про Meshy и Tripo здесь не нужен. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `decal_oil_stain` | декаль с прозрачностью | 512² | масляное пятно |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/decals/`.

```text
Concept art reference for an oil stain on asphalt seen from directly above: an irregular dark pool with a faint rainbow sheen at the edges and a few drips at the rim. Top-down view, even daylight, square image, no people, no text, no logos.
```

## 3. Текстура декали (генератор изображений)

### 3.1. `decal_oil_stain`

```text
Oil stain decal on a plain background: an irregular dark pool, almost black in the centre, with a faint rainbow sheen at the edges, seen from directly above, square image, no text.
```

Фон вырезаем после генерации, декаль сохраняем с прозрачностью.

## 4. Наши требования

- Декаль — текстура на плоскости, 2 треугольника. Бюджет треугольников не применяется.
- Радужный блеск — часть текстуры. На светлом покрытии (снег, песок) блеск слабее, это решает шейдер.
- PNG 512², с альфой.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `decal_oil_stain.png` | | | |

## Открытые вопросы

Открытых вопросов по текстуре нет. Вопросы по поведению — в [README](README.md).
