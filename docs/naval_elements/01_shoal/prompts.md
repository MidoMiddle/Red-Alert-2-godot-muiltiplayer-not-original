# 01. Мель: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Мель](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промт для картинки-референса и текстуры дна мели. Моделей нет: мель — тип клетки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `shoal_sand` | текстура, бесшовная | 1024² | песчаное дно мели (видно сквозь мелкую воду) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for a sandbar in shallow sea water in bright summer daylight: a pale sandy bottom visible through turquoise water, ripples on the sand, darker deep water beyond the bar. Top-down view close to 45 degrees, realistic materials, square image, no people, no text.
```

## 3. Текстура (генератор изображений, не 3D)

### 3.1. `shoal_sand`

```text
Seamless tileable texture of pale yellow sea sand on a shallow seabed with small ripples, seen through clear turquoise water. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстура — дно под водой. Вода сверху — шейдер, текстура её не закрывает.
- Тип клетки — `Shallow` из `terrain.yaml`, новых типов не вводим.
- Цвета команды нет.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `shoal_sand.png` | | | |

## Открытые вопросы

Открытых вопросов по текстуре нет.
