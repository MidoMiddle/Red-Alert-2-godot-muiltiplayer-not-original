# 16. Границы карты: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Границы карты](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и фонового панорамного изображения за кулисами. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

3D-моделей нет. Край карты — это кулисы (рельеф и декор) и фон за ними. Фон — одна панорама:

| Ассет | Тип | Размер | Назначение |
|---|---|---|---|
| `bg_horizon_panorama` | панорама, 2:1 | 2048×1024 | дальний фон за кулисами |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/bounds/`.

```text
Concept art reference for the edge of a winter map: snowy ground fading into a grey-blue mist, distant snowy hills barely visible through the fog, soft even light. No buildings, no people, no text, wide landscape image.
```

## 3. Панорама (генератор изображений)

### 3.1. `bg_horizon_panorama`

```text
A wide panoramic winter landscape: distant snowy hills and a misty treeline fading into pale grey-blue fog, soft overcast sky, no buildings, no people, photorealistic, 2:1 aspect ratio, no text.
```

## 4. Наши требования

- Панорама — фон, не часть симуляции. Она стоит за кулисами, где карты уже нет.
- Соотношение 2:1, 2048×1024 (предложение). Без швов по горизонтали, если панорама повторяется по кругу.
- Без зданий и людей, без надписей.
- Туман у горизонта совпадает с туманом у края карты в Godot, иначе край будет видно.
- Цвет команды не нужен.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `bg_horizon_panorama.png` | | | |

## Открытые вопросы

1. Нужен ли горизонт вообще, или туман у края может быть бесконечным (без панорамы).
