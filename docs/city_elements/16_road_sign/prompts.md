# 16. Дорожный знак: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Дорожный знак](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса, лица знаков и связи с моделью знака на карте. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `prop_road_sign` | модель | футпринт 1x1, высота 1,5 | стойка и щит; та же модель, что на карте |
| `sign_stop`, `sign_yield`, `sign_pedestrians` | текстура лица знака, 2D | 512² | лицо щита (узел `sign_face`) |

Модель стойки и щита — та же, что `prop_road_sign` в [09_props](../../map_elements/09_props/prompts.md). Промт и нормализацию берём оттуда, второй раз не генерируем.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for road signs by a city road in neutral daylight: a red octagonal stop sign, a red-bordered triangle sign, and a blue square pedestrian sign, all on thin metal posts. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text.
```

## 3. Лица знаков (генератор изображений)

Текста на знаках нет, только символы и форма. Так знаки не зависят от языка.

### 3.1. `sign_stop`

```text
A flat road sign face shaped as a red octagon with a thin white border, plain flat colours, no text, no letters, centred, plain background, square image.
```

### 3.2. `sign_yield`

```text
A flat road sign face shaped as a white triangle with a thick red border, pointing down, plain white centre, no text, no letters, centred, plain background, square image.
```

### 3.3. `sign_pedestrians`

```text
A flat road sign face: a blue square with a thin white border and a simple white pictogram of a walking person, flat colours, no text, centred, plain background, square image.
```

## 4. Наши требования

- **Лицо знака** — отдельный узел `sign_face` в модели. Текстуру меняем по типу знака, модель одна.
- **Пивот** — центр клетки на земле, стойка до 1,5 высоты.
- **Не блокирует проход.** Знаки стоят у обочины.
- **Цвета** — красный, синий, белый, жёлтый. Совпадают с appearance-описанием знаков.
- **Номера скорости не делаем**: цифры зависят от языка и требуют отдельного шрифта.
- **Бюджет** ≤ 800 треугольников (из модели `prop_road_sign`), лица 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `sign_stop.png` | | | |
| `sign_yield.png` | | | |
| `sign_pedestrians.png` | | | |

Модель `prop_road_sign.glb` записана в [09_props](../../map_elements/09_props/prompts.md), здесь не повторяем.

## Открытые вопросы

1. Влияют ли знаки на движение мирного транспорта. Если да, нужна логика, а не только картинка.
2. Нужен ли знак поворота (синий квадрат со стрелкой) как отдельное лицо.
