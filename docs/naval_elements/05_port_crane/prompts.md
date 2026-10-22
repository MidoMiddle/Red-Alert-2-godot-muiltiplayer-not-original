# 05. Портовый кран: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Портовый кран](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели крана. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `port_crane` | объект | 1x1 | 5 | ≤ 4 000 | `base`, `tower`, `boom` | основание |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for a port tower crane on a harbour quay in bright summer daylight: a yellow steel lattice tower on a grey concrete base, a long boom reaching out over the quay, a small cab at the top. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `port_crane`

```text
A harbour tower crane about five units tall: a yellow steel lattice tower on a grey concrete base, a long white-striped boom reaching out sideways, a small dark glass cab at the top. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`. Узлы: `base` (основание), `tower` (башня), `boom` (стрела, вращается в коде).

## 4. Наши требования

- Пивот — центр клетки на земле.
- Блокирует только основание. Цвета команды нет.
- Стрела — отдельный узел с осью вращения на башне.
- Бюджет ≤ 4 000 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `port_crane.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
