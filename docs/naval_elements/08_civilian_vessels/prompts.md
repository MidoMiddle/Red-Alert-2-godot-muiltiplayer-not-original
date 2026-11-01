# 08. Гражданские суда: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Гражданские суда](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и моделей паромов и грузового судна. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `civ_ferry` | судно | 2x1 | 0,95 (труба) | ≤ 8 000 | флот |
| `civ_cargo_ship` | судно | 4x1 | 1,1 (труба) | ≤ 8 000 | флот |
| `civ_fishing_boat` | судно | 1x1 | 0,62 (мачта) | ≤ 1 500 | флот |

Рыбацкая лодка сделана первой (2026-11-01): `civ_fishing_boat`. Её корпус — основа будущей лодки на песке [beached_boat](../../beach_elements/17_beached_boat/prompts.md). Покачивание добавляем в коде.

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

- Узлы: `hull` (корпус, именование как у техники, M1 4.7), `cabin` (надстройка), `funnel` (труба, дымит), `smoke_anchor` (верх трубы, у целых), `fire_1`, `fire_2` (у повреждённых).
- Размер игры: паром 1,9 × 0,6, грузовое 3,8 × 0,76, лодка 0,9 × 0,34. Нос к +x.
- Состояния: целое и повреждённое (горит). Тонет — код игры кренит и погружает повреждённое.
- Пивот — центр футпринта на уровне воды.
- Надписей, флагов и военных признаков нет.
- Цвета команды нет: суда нейтральные.
- Бюджет: паром и грузовое ≤ 8 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_ferry.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_vessels.py`) | 2026-11-01 | сделано, 1 662 треугольников |
| `civ_ferry_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_vessels.py`) | 2026-11-01 | сделано, 1 698 треугольников |
| `civ_cargo_ship.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_vessels.py`) | 2026-11-01 | сделано, 1 150 треугольников |
| `civ_cargo_ship_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_vessels.py`) | 2026-11-01 | сделано, 1 294 треугольников |
| `civ_fishing_boat.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_vessels.py`) | 2026-11-01 | сделано, 398 треугольников |
| `civ_fishing_boat_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/naval_vessels.py`) | 2026-11-01 | сделано, 446 треугольников |

## Открытые вопросы

Открытых вопросов по моделям нет.
