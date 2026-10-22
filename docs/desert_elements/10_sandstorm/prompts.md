# 10. Песчаная буря: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Песчаная буря](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промт только для картинки-образца: буря — эффект клиента, 3D-моделей нет. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Пустыня». Промт на английском.

---

## 1. Что нужно сделать

Моделей и текстур нет. Буря — частицы и пост-эффекты в Godot ([18_lighting](../../map_elements/18_lighting/prompts.md), раздел «Погода»). Нужна одна картинка-образец для художника.

## 2. Картинка-образец (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/desert/`. В игре картинки не будет, она нужна для настройки шейдера и частиц.

```text
Concept art reference for a desert sandstorm approaching across a flat plain in harsh light: a wall of ochre dust on the horizon, yellow-orange sky, sand streaks in the air, distant rocks half hidden by dust. Wide landscape image, realistic, no people, no text, no logos.
```

## 3. Модели и текстуры

Нет. Пылевой фронт, частицы песка и затемнение — шейдер и частицы в Godot.

## 4. Наши требования

- Видимость и цвет неба — параметры климата в данных.
- Пыль — частицы и постобработка, не текстура на всём экране.
- Цвета команды нет.

## 5. Реестр

Записей нет: модели и текстуры не создаём.

## Открытые вопросы

1. Снижает ли буря обзор (см. [README](README.md)).
