# 21. Тонкий лёд: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Тонкий лёд](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса, текстуры тонкого льда и трещин. Общие правила — в [требованиях](../generation_requirements.md). Стиль — зимний климат: холодный дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `ice_thin` | текстура, бесшовная | 1024² | тонкий лёд: прозрачность, тёмная вода под льдом |
| `ice_crack_decal` | декаль с прозрачностью | 512² | трещины поверх льда (по желанию) |
| `ice_chunk` | объект | 0,3 высоты, ≤ 200 треугольников | обломки льда после пролома (по желанию) |

Пролом сам — вода (шейдер), в модели её нет.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/winter/`.

```text
Concept art reference for thin ice on a frozen lake in winter daylight: pale translucent blue ice with dark water patches visible underneath, fine radial cracks, a broken hole with jagged ice chunks floating in dark water. Top-down view close to 45 degrees, realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений)

### 3.1. `ice_thin`

```text
Seamless tileable texture of thin translucent pale blue lake ice with dark water patches showing through, fine white cracks, slight snow dusting at the edges. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `ice_crack_decal`

```text
Crack pattern decal on a transparent background: thin white radial cracks spreading from a centre point, irregular, no fill, square image, no text.
```

Фон вырезаем после генерации, декаль с прозрачностью.

## 4. Модель (по желанию, Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 4.1. `ice_chunk`

```text
A jagged chunk of thin pale translucent lake ice about three tenths of a unit tall, a few cracks, a little snow on top. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 5. Наши требования

- Текстура льда бесшовная, 1024², PNG. Прозрачность льда — материал, не текстура.
- Тип клетки — `Ice`, тонкость — маска карты. Текстура не решает, ломается ли лёд.
- Трещины — декаль, не геометрия.
- Цвета команды нет.

## 6. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `ice_thin.png` | | | |
| `ice_crack_decal.png` | | | |
| `ice_chunk.glb` (по желанию) | | | |

## Открытые вопросы

Открытых вопросов по текстуре нет.
