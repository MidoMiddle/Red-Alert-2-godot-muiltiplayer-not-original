# 12. Маяк: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Маяк](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели маяка. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|---|
| `beach_lighthouse` | здание | 2x2 | 7 | ≤ 10 000 | `body`, `gallery`, `lantern` | нет |

Свет фонаря — emissive-материал в Godot, в модели стекло погашено.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for a tall white lighthouse on a sandy headland in bright summer daylight: a tapering tower with horizontal red bands, a metal railing gallery near the top, and a glass lantern room with a dark dome. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `beach_lighthouse`

```text
A tall tapering lighthouse about seven units tall: a white tower with horizontal red bands, a metal railing gallery near the top, a glass lantern room with a dark metal dome on top, the glass unlit. No text, no symbols. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, dry weather, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- Пивот — центр футпринта на земле. Узлы: `body` (башня), `gallery` (галерея с перилами), `lantern` (фонарь, свет — в Godot).
- Гарнизон 4 — внутри винтовая лестница. Для этого геометрия внутри башни не нужна, ставим только внешнюю оболочку.
- Цвета полос — текстура. Цвета команды нет.
- Бюджет ≤ 10 000 треугольников, текстуры 2048² (высокое здание).
- Прочность высокая (камень, данные).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `beach_lighthouse.glb` | | | |

## Открытые вопросы

1. Должен ли маяк быть неразрушаемым ориентиром (как церковь в городе).
