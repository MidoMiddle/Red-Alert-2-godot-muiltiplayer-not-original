# Черновики зданий в YAML

> Статус: черновик · Создан: 2026-10-23
> Связанные: [обзор зданий](../README.md) · [очерёдность открытия](../tech_tree.md) · [формат данных](../../formats/data.md)

Это черновики записей о зданиях в формате `game/data`. **Игра их не загружает.** Загрузчик читает всё в `game/data/`, а здания Альянса, Культа и нейтральные пока без поведения (вехи M7 и M11). Поэтому черновики лежат здесь и переходят в игру по мере вех.

## Что здесь

| Файл | Записей | Что это |
|---|---|---|
| `union/buildings.yaml` | 7 | Союз: здания, которых нет в `game/data` (MVP-11 уже в игре) |
| `alliance/buildings.yaml` | 20 | Альянс, по карточкам `allies/` |
| `cult/buildings.yaml` | 16 | Культ (YR), по карточкам `yuri/` |
| `neutral/buildings.yaml` | 11 | Нейтральные и технические, ID как в M7 (`src/Rts.Simulation/World/BuildingTypes.cs`) |

Не включены две RA2-версии Советов: `cloning_vats` и `psychic_sensor`. В YR они перешли Культу (клонирование закреплено за Культом, пси-сенсор стал пси-радаром Юрия). Из 65 карточек: 11 уже в игре, 52 здесь, 2 исключены.

## Классы поведения

- **a** — поведение есть в схеме M3/M4, перенос возможен, когда фракция появится в игре.
- **b** — нужна веха, которая добавит поведение (M4 или M11). Причина — в комментарии `# M11…` у записи.
- **c** — поведения в вехах нет. Причина — в комментарии `# не описано…`.

| Фракция | a | b | c |
|---|---|---|---|
| Союз | 0 | 4 | 3 |
| Альянс | 8 | 9 | 3 |
| Культ | 2 | 10 | 4 |
| Нейтральные | 1 | 8 | 2 |

## Соглашения

- Схема — [docs/formats/data.md](../../formats/data.md). `inherits` берётся из `game/data/shared/templates.yaml` (`base_building`, `base_defense`).
- ID: Союз — без префикса (`barracks`, `reactor`); Альянс — `alliance_*`; Культ — `cult_*`; нейтральные — `tech_*` и `neutral_*`.
- `null` с `# TODO` — значение не подтверждено источниками или схема его не принимает. Загрузчик на `null` упадёт, поэтому при переносе сначала заполняем.
- `build_time` — стоимость × 0,042 с: правило шаблонов `game/data/shared/templates.yaml` (1000 кредитов = 42 с).
- Пререквизиты — минимальный набор прямых: то, что уже требует другое здание из списка, не дублируется (как в `game/data/factions/union/buildings.yaml`).
- Стоимость, энергия и прочность — из правил YR (`rulesmd.ini`), размеры — из карточек. Если карточка и правила расходятся, это TODO.
- Нейтральные не строятся: стоимость и время постройки — `null` (TODO). Схема требует стоимость больше нуля, это решается при переносе.

## Как перенести в game/data

1. Фракция должна быть в `game/data/factions/factions.yaml` (с `mcv`, `construction_yard`, `refinery`, `harvester`). Без неё здания не загрузятся.
2. Запись переходит в `game/data/factions/<фракция>/buildings.yaml`. Для Союза — в существующий файл.
3. Все `null` заполняются, `# TODO` убираются.
4. Обновляются `tests/Rts.Data.Tests/Rules/GameDataTests.cs` (счётчики и зафиксированный хэш данных) — в том же коммите.
5. Проверка: `dotnet run --project tools/Rts.MapTool -- rules` и `dotnet test tests/Rts.Data.Tests`.
6. Записи класса b и c переносим, когда вехи из комментария добавили поведение.

## Соответствие ID

Карточка — папка в `docs/ra2_buildings/`. Правила — внутренний ID записи в `rulesmd.ini` (YR), по нему проверяются числа.

| ID | Карточка | Правила | Класс |
|---|---|---|---|
| `naval_shipyard` | `soviets/naval_shipyard` | `NAYARD` | b |
| `service_depot` | `soviets/service_depot` | `NADEPT` | c |
| `nuclear_reactor` | `soviets/nuclear_reactor` | `NANRCT` | c |
| `iron_curtain` | `soviets/iron_curtain` | `NAIRON` | b |
| `nuclear_missile_silo` | `soviets/nuclear_missile_silo` | `NAMISL` | b |
| `industrial_plant` | `soviets/industrial_plant` | `NAINDP` | c |
| `battle_bunker` | `soviets/battle_bunker` | `NABNKR` | b |
| `alliance_construction_yard` | `allies/construction_yard` | `GACNST` | a |
| `alliance_power_plant` | `allies/power_plant` | `GAPOWR` | a |
| `alliance_barracks` | `allies/barracks` | `GAPILE` | a |
| `alliance_ore_refinery` | `allies/ore_refinery` | `GAREFN` | a |
| `alliance_war_factory` | `allies/war_factory` | `GAWEAP` | a |
| `alliance_air_force_command` | `allies/air_force_command` | `GAAIRC` | b |
| `alliance_naval_shipyard` | `allies/naval_shipyard` | `GAYARD` | b |
| `alliance_service_depot` | `allies/service_depot` | `GADEPT` | b |
| `alliance_battle_lab` | `allies/battle_lab` | `GATECH` | a |
| `alliance_ore_purifier` | `allies/ore_purifier` | `GAOREP` | c |
| `alliance_spy_satellite_uplink` | `allies/spy_satellite_uplink` | `GASPYSAT` | c |
| `alliance_robot_control_center` | `allies/robot_control_center` | `GAROBO` | c |
| `alliance_chronosphere` | `allies/chronosphere` | `GACSPH` | b |
| `alliance_weather_control_device` | `allies/weather_control_device` | `GAWEAT` | b |
| `alliance_pillbox` | `allies/pillbox` | `GAPILL` | a |
| `alliance_patriot_missile_system` | `allies/patriot_missile_system` | `NASAM` | b |
| `alliance_prism_tower` | `allies/prism_tower` | `ATESLA` | b |
| `alliance_gap_generator` | `allies/gap_generator` | `GAGAP` | b |
| `alliance_grand_cannon` | `allies/grand_cannon` | `GTGCAN` | b |
| `alliance_wall` | `allies/wall` | `GAWALL` | a |
| `cult_construction_yard` | `yuri/construction_yard` | `YACNST` | a |
| `cult_bio_reactor` | `yuri/bio_reactor` | `YAPOWR` | c |
| `cult_barracks` | `yuri/barracks` | `YABRCK` | a |
| `cult_slave_miner` | `yuri/slave_miner` | `YAREFN` | b |
| `cult_war_factory` | `yuri/war_factory` | `YAWEAP` | b |
| `cult_psychic_radar` | `yuri/psychic_radar` | `NAPSIS` | b |
| `cult_submarine_pen` | `yuri/submarine_pen` | `YAYARD` | b |
| `cult_grinder` | `yuri/grinder` | `YAGRND` | b |
| `cult_cloning_vats` | `yuri/cloning_vats` | `NACLON` | b |
| `cult_battle_lab` | `yuri/battle_lab` | `YATECH` | c |
| `cult_gattling_cannon` | `yuri/gattling_cannon` | `YAGGUN` | b |
| `cult_psychic_tower` | `yuri/psychic_tower` | `YAPSYT` | b |
| `cult_tank_bunker` | `yuri/tank_bunker` | `NATBNK` | b |
| `cult_genetic_mutator` | `yuri/genetic_mutator` | `YAGNTC` | b |
| `cult_psychic_dominator` | `yuri/psychic_dominator` | `YAPPET` | c |
| `cult_citadel_wall` | `yuri/citadel_wall` | `GAFWLL` | c |
| `tech_oil_derrick` | `tech/oil_derrick` | `CAOILD` | b |
| `tech_hospital` | `tech/hospital` | `CATHOSP` | b |
| `tech_airfield` | `tech/airport` | `CAAIRP` | b |
| `tech_machine_shop` | `tech/machine_shop` | `CAMACH` | b |
| `tech_secret_lab` | `tech/secret_lab` | `CASLAB` | c |
| `tech_power_plant` | `tech/power_plant` | `CAPOWR` | b |
| `tech_outpost` | `tech/outpost` | `CAOUTP` | c |
| `tech_bridge_repair_hut` | `tech/bridge_repair_hut` | `CABHUT` | b |
| `neutral_shed` | `—` | `—` | a |
| `neutral_house_a` | `—` | `—` | b |
| `neutral_apartment` | `—` | `—` | b |

## Открытые вопросы

- `null` с TODO по файлам: union 4, alliance 24, cult 23, neutral 37.
- `exit_cell` и `dock_cell` — из `art.ini`, не заполнены (`null`).
- `primary` оборонительных зданий — оружие из `weapons.yaml` (M4), сейчас `null`.
- Броня гражданских зданий в правилах не указана (`null`).
- Схема: стоимость нейтральных зданий должна быть больше нуля, а верфи в правилах имеют `adjacent` 12 при допустимом 0–10. Решается до переноса (схема или значения).
- Нейтральные больница и аэродром: в M7 код задаёт другие размер и прочность, чем правила или карточки. Записи помечены TODO, решение — при переносе M7 14а.
- Юнитные черновики в `docs/ra2_units/yaml_drafts/` ссылаются на ID зданий Союза без префикса. При переносе Альянса и Культа ссылки там надо поправить; эта папка принадлежит другой сессии.
