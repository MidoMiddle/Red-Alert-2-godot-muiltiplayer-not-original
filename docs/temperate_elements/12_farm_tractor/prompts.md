# 12. Трактор: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Трактор](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели трактора. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `civ_tractor` | техника (гражданская) | 1x1 | 1,0 | ≤ 4 000 | `hull`, `wheel_rear`, `wheel_front` | да, как техника |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a farm tractor in warm daylight: a bright red compact tractor with large rear wheels, smaller front wheels, a grey roll bar cab over the seat, an exhaust pipe. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `civ_tractor`

```text
A compact farm tractor about one unit long and one unit tall: a bright red body, large rear wheels with black tyres, smaller front wheels, a grey roll bar cab over the seat, an exhaust pipe. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 1x1`. Колёса — отдельные узлы для вращения.

## 4. Наши требования

- Пивот — центр основания на земле (как у техники, M1, 4.7). Узлы: `hull`, `wheel_rear`, `wheel_front`.
- Цвет корпуса — варианты текстуры. Цвета команды нет.
- Бюджет ≤ 4 000 треугольников (гражданская техника, меньше боевой).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_tractor.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
