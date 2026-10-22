# 01. Песок и барханы: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Песок и барханы](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и текстуры песка. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня»: жёсткий яркий солнечный свет, пыль. Промты на английском.

---

## 1. Что нужно сделать

Модели нет: барханы — рельеф из кода. Нужна одна текстура.

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `desert_sand` | текстура, бесшовная | 1024² | песок пустыни (тип «бездорожье») |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`.

```text
Concept art reference for a desert of fine sand dunes in harsh bright sunlight: pale orange sand with wind ripples, a ridge of dunes casting a sharp shadow, dust haze in the air. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Текстура (генератор изображений, не 3D)

### 3.1. `desert_sand`

```text
Seamless tileable texture of fine pale orange desert sand with soft wind ripples and a few darker grains. Photorealistic, top-down orthographic view, even lighting with no shadows, 1024x1024, no text.
```

## 4. Наши требования

- Текстура бесшовная, 1024², PNG, без запечённых теней.
- Тип земли — «бездорожье». Текстура — только визуал.
- Масштаб тайлинга подбираем в Godot.
- Цвета команды нет.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `desert_sand.png` | | | |

## Открытые вопросы

Открытых вопросов по текстуре нет.
