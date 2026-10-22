# 06. Колодец: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Колодец](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели колодца. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Умеренный»: тёплый дневной свет, без снега. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Блокирует |
|---|---|---|---|---|---|---|
| `well` | объект | 1x1 | 1,2 | ≤ 1 500 | `body`, `roof`, `winch` | да, всех |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/temperate/`.

```text
Concept art reference for a village well in warm daylight: a round grey stone well with a wooden roof on two posts, a winch and a bucket on a rope, a little moss on the stones. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `well`

```text
A round grey stone village well about one unit tall with a wooden roof on two posts, a winch with a bucket on a rope, a little moss on the stones. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, warm daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `body` (сруб), `roof` (крыша), `winch` (ворот).
- Вода в срубе — шейдер, не геометрия.
- Цвета команды нет.
- Бюджет ≤ 1 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `well.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
