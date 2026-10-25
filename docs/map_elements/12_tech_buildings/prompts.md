# 12. Техздания: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Техздания](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и моделей техзданий: нефтевышка, госпиталь, мастерская, аэродром, электростанция, будка ремонта моста. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Узлы | Цвет команды |
|---|---|---|---|---|---|
| `building_oil_derrick` | 2x2 | 2 | ≤ 8 000 | `body`, `beam`, `team_mask` | да (кольцо на силосе) |
| `building_hospital` | 3x3 | 3 | ≤ 12 000 | `body`, `roof`, `team_mask` | да (полоса у входа) |
| `building_workshop` | 3x2 | 3,5 | ≤ 12 000 | `body`, `roof`, `team_mask` | да (полоса над воротами) |
| `building_airport` | 4x4 | 4 | ≤ 20 000 | `body`, `roof`, `team_mask` | да (на башне) |
| `building_power_station` | 3x3 | 4 | ≤ 12 000 | `body`, `team_mask` | да (на диспетчерской) |
| `building_bridge_booth` | 1x1 | 1,5 | ≤ 2 000 | `body`, `team_mask` | да (на крыше) |

Самолёты на аэродроме — отдельная задача M11, здесь не делаем. Пар от градирен и огни — эффекты в Godot.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/buildings_tech/`.

```text
Concept art reference for a winter industrial outskirts: a steel lattice oil derrick with a pumpjack beam, a two-storey hospital with large windows, a hangar-style workshop with an overhead crane, a power station with two concrete cooling towers, and a small repair booth at a bridge end. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text, no emblems or symbols on the walls.
```

## 3. Модели (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `building_oil_derrick`

```text
A steel lattice oil derrick tower on a concrete base, a pumpjack with a nodding beam on one side, a small pump house at the foot, snow on the metal. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`. Качалку отделить в Blender как узел `beam` с пивотом в её оси вращения. Генератор этого не сделает сам.

### 3.2. `building_hospital`

```text
A two-storey hospital building with a flat roof and large windows, a canopy over the entrance, a rooftop air unit, clean white and grey walls, no emblems or symbols on the walls. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`.

### 3.3. `building_workshop`

```text
A large hangar-style workshop with a gabled roof, big sliding doors, an overhead crane on the roof, stacks of crates outside, snow on the roof. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x2`.

### 3.4. `building_airport`

```text
A small airfield complex: a long hangar with a gabled roof, a four-storey control tower with a glass cabin on top, a concrete apron in front, snow on everything. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building complex on a plain light grey background.
```

Нормализация: `--kind building --footprint 4x4`.

### 3.5. `building_power_station`

```text
A small power station: two concrete cooling towers, a brick control house, pipes and a transformer yard behind a low fence. No steam and no smoke, these are effects. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, cold winter setting, single isolated building complex on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`.

### 3.6. `building_bridge_booth`

```text
A small wooden repair booth with a sloped roof covered in snow, one window, a tool shed beside it, and a plain lamp on the roof. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле (M1, 4.7).
- **Цвет команды** — захваченное техздание получает цвет владельца. Маска — флаг, полоса или пластина на крыше, как в модели. Цвет задаёт параметр экземпляра (M1, 4.6).
- **Лампа** будки и огни на крыше — emissive в Godot, в модели погашены.
- **Качалка нефтевышки** — отдельный узел `beam`. Без него анимация качалки не работает.
- **Текстуры:** 2048² для больших зданий (аэродром, мастерская, госпиталь), 1024² для будки.
- **Символы и эмблемы** на стенах не делаем: госпиталь без знаков, чтобы не копировать реальные символы.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `building_oil_derrick.glb` (в игре `tech_oil_derrick.glb`) | Blender 5.2.3, вручную (скрипт `tools/blender/families/tech_derrick.py`) | 2026-10-25 | сделано: целая (качалка качается), повреждённая с огнём, руины |
| `building_hospital.glb` | | | |
| `building_workshop.glb` | | | |
| `building_airport.glb` | | | |
| `building_power_station.glb` | | | |
| `building_bridge_booth.glb` | | | |

## Открытые вопросы

1. Показывать ли зону действия госпиталя кругом на земле.
2. Есть ли у госпиталя ещё одна модель: с ранеными на крыльце (визуальная деталь, не обязательная).
