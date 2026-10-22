# 08. Гражданские суда: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Гражданские суда](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей паромов и грузового судна. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `civ_ferry` | судно | 2x1 | 1,2 | ≤ 8 000 | флот |
| `civ_cargo_ship` | судно | 4x1 | 1,5 | ≤ 8 000 | флот |
| рыбацкая лодка | судно | 1x1 | 0,6 | — | флот |

Рыбацкую лодку берём из [beached_boat](../../beach_elements/17_beached_boat/prompts.md): корпус тот же, плавучесть и покачивание добавляем в коде.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for civilian ships in bright summer daylight: a white two-deck ferry with a blue stripe and a funnel, next to a long grey cargo ship with a crane on deck and no writing on its hull. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated objects on a plain light grey background, realistic materials, square image, no people, no text, no logos, no flags.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `civ_ferry`

```text
A two-deck ferry about two units long and one unit wide: a white hull with a blue stripe, grey decks, a short funnel, a small ramp at the bow, no writing and no flags. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 2x1`.

### 3.2. `civ_cargo_ship`

```text
A long grey cargo ship about four units long and one unit wide: a dark red hull, a white superstructure at the stern, a crane on deck, no writing and no flags. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind vehicle --footprint 4x1`.

## 4. Наши требования

- Узлы: `hull` (корпус, именование как у техники, M1 4.7), `cabin` (надстройка), `funnel` (труба, дымит).
- Пивот — центр футпринта на уровне воды.
- Надписей, флагов и военных признаков нет.
- Цвета команды нет: суда нейтральные.
- Бюджет: паром и грузовое ≤ 8 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_ferry.glb` | | | |
| `civ_cargo_ship.glb` | | | |

## Открытые вопросы

Открытых вопросов по моделям нет.
