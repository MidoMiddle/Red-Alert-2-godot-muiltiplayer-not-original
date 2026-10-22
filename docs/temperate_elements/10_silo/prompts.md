# 10. Силос: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Силос](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели силоса. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `temperate_silo` | здание | 1x1 | 5 | ≤ 3 000 | `body`, `dome`, `ladder` | да, всех |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a tall grain silo on a farm in warm daylight: a light grey concrete cylinder with metal hoops, a dark domed roof on top, a rusty ladder along the side. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `temperate_silo`

```text
A tall grain silo about five units tall: a light grey concrete cylinder with dark metal hoops, a dark domed roof on top, a rusty ladder running up one side. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `body`, `dome`, `ladder`.
- Блокирует всех наземных. Цвета команды нет.
- Бюджет ≤ 3 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `temperate_silo.glb` | | | |

## Открытые вопросы

1. Пыльный взрыв силоса (см. [README](README.md)).
