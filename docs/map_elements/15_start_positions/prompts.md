# 15. Стартовые позиции и точки маршрута: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Стартовые позиции и точки маршрута](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса, иконки маркера старта и кольца на земле. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

Модели в игре нет: сама позиция — это место на земле. Сборочный фургон (MCV) — юнит, его описываем в документе юнита. Здесь только 2D-картинки:

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `icon_start_marker` | иконка с прозрачностью | 512² | редактор, превью карты, мини-карта |
| `decal_start_ring` | текстура, бесшовная, по желанию | 512² | кольцо под стартом; цвет команды задаёт шейдер |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/starts/`.

```text
Concept art reference for a starting position in a winter map: a flat clearing with trampled snow in a circle, a short wooden stake with a plain white flag on top, empty ground around. Mix of top-down and three-quarter view, realistic materials, square image, no text, no numbers.
```

## 3. Иконки и текстуры (генератор изображений, не 3D)

### 3.1. `icon_start_marker`

```text
A flat icon of a circular map marker with a simple white flag on a short pole, bold dark outline, flat colours, centred, plain background, no text, no numbers, square image.
```

Нужно вырезать фон и сохранить с прозрачностью. Номер старта рисует интерфейс, не иконка.

### 3.2. `decal_start_ring` (по желанию)

```text
Seamless circular decal texture: a trampled snow ring with soft edges, top-down view, neutral white and grey, alpha-friendly, no text, square image.
```

## 4. Наши требования

- **Цвет команды** — параметр экземпляра шейдера (M1, 4.6). В иконке и в кольце цвет не запекаем.
- **Номер старта** — текст интерфейса, поэтому в иконке цифр нет. Так номера не зависят от шрифта.
- **Ровная площадка** — сама позиция в геймплее: ровный участок без деревьев и камней. Это правило данных карты (M7), не модели.
- **Размер иконки:** 512² с прозрачностью. На экране иконка меньше, оставляем запас для чёткости.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `icon_start_marker.png` | | | |
| `decal_start_ring.png` (по желанию) | | | |

## Открытые вопросы

1. Нужна ли отдельная иконка для превью карты, если на мини-карте используется та же.
2. Подсвечивать ли старт в начале партии (сейчас вопрос из [appearance.md](appearance.md)).
