# 03. Поле: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Поле](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстур полей. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

Модели нет: поле — текстура поверх типа «чисто».

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `field_wheat` | текстура, бесшовная | 1024² | засеянное поле (посев) |
| `field_plowed` | текстура, бесшовная | 1024² | вспаханная пашня |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for farm fields in warm daylight: a field of golden wheat with visible rows and a neighbouring freshly ploughed brown field with furrows. Top-down oblique view close to 45 degrees, realistic materials, square image, no people, no text.
```

## 3. Текстуры (генератор изображений, не 3D)

### 3.1. `field_wheat`

```text
Seamless tileable texture of a wheat field with rows of golden-green ears of grain, slight wind streaks. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

### 3.2. `field_plowed`

```text
Seamless tileable texture of freshly ploughed brown soil with parallel furrows, clods of earth, a few small stones. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстуры бесшовные, 1024², PNG, без запечённых теней.
- Тип земли — «чисто». Текстура — только визуал.
- Направление рядов в текстуре совпадает во всех полях одного клочка (иначе видна стыковка).
- Цвета команды нет.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `field_wheat.png` | | | |
| `field_plowed.png` | | | |

## Открытые вопросы

1. Варианты созревания посева (зелёный, золотой, скошенный) — нужны ли отдельные текстуры.
