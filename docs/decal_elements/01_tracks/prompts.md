# 01. Следы техники: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Следы техники](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстур декалей. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Декаль — не модель, поэтому раздел 3 требований про Meshy и Tripo здесь не нужен. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `decal_tracks_wheel` | декаль с прозрачностью | 512² | колея колёс |
| `decal_tracks_track` | декаль с прозрачностью | 512² | след гусениц |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/decals/`.

```text
Concept art reference for vehicle tracks in fresh snow seen from directly above: two parallel tyre ruts with a tread pattern and a pair of wide caterpillar tread marks, dark grey on white snow with soft edges. Top-down view, even daylight, square image, no people, no text, no logos.
```

## 3. Текстуры декалей (генератор изображений)

### 3.1. `decal_tracks_wheel`

```text
Tyre track decal on a plain background: two parallel tyre ruts with a tread pattern, dark grey with soft edges, seen from directly above, square image, no text.
```

### 3.2. `decal_tracks_track`

```text
Caterpillar track decal on a plain background: two parallel wide tread marks with transverse ribs, dark grey with soft edges, seen from directly above, square image, no text.
```

Фон вырезаем после генерации, декаль сохраняем с прозрачностью.

## 4. Наши требования

- Декаль — текстура на плоскости, 2 треугольника. Бюджет треугольников не применяется.
- Цвет серый: шейдер окрашивает след под покрытие. Цветные варианты не делаем.
- PNG 512², с альфой. Края мягкие, без жёсткой обрезки.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `decal_tracks_wheel.png` | | | |
| `decal_tracks_track.png` | | | |

## Открытые вопросы

Открытых вопросов по текстурам нет. Вопросы по поведению — в [README](README.md).
