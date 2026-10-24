# Имена в игре: рабочий список

Единый список «id в данных → имя на экране». Имена — свои, английские, **рабочие**: финальные выбирает владелец позже, вместе с названиями фракций и игры (M11, раздел 9; [15_legal.md](../15_legal.md) — имена EA в игру не попадают). Аналог RA2 — только для справки разработчикам, на экран не выводится.

«Позже» — особая способность здания ждёт механики M11. Здание уже строится, потребляет энергию и открывает дерево технологий; в подсказке сайдбара написано, что способность появится позже (`ability_later` в данных).

## Союз (`union`)

| id | Имя | Роль | Аналог RA2 | Способность |
|---|---|---|---|---|
| construction_yard | Construction Yard | двор, из MCV | Construction Yard | работает |
| reactor | Reactor | энергия +150 | Tesla Reactor | работает |
| refinery | Refinery | ОПЗ, харвестер | Ore Refinery | работает |
| barracks | Barracks | пехота | Barracks | работает |
| war_factory | War Factory | техника | War Factory | работает |
| radar | Radar | миникарта | Radar Tower | работает |
| tech_center | Tech Center | высокий тех | Battle Lab | работает |
| wall | Wall | стены | Wall | работает |
| sentry_bunker | Sentry Bunker | пулемёт | Sentry Gun | работает |
| tesla_coil | Tesla Coil | электрооборона | Tesla Coil | работает |
| flak_cannon | Flak Cannon | ПВО | Flak Cannon | работает (по авиации — M11b) |
| naval_shipyard | Dockyard | верфь | Naval Shipyard | работает: стоит на глубокой воде, строит корабли (этап 2а); ремонт кораблей позже |
| service_depot | Field Workshop | ремонт техники | Service Depot | позже |
| nuclear_reactor | Atomic Plant | энергия +2000 | Nuclear Reactor | энергия работает; взрыв при гибели позже |
| iron_curtain | Bastion Field Projector | неуязвимость группе | Iron Curtain | позже (M11a) |
| nuclear_missile_silo | Strategic Missile Silo | ракетный удар | Nuclear Missile Silo | позже (M11a) |
| industrial_plant | Heavy Industry Complex | скидка на технику | Industrial Plant | позже |
| battle_bunker | Fortified Bunker | гарнизон | Battle Bunker | позже (M11a) |

Имена первых одиннадцати пришли из M3 и тоже рабочие: «Tesla Coil», «War Factory», «Flak Cannon» совпадают с RA2 и заменяются вместе с остальными.

## Альянс (`alliance`)

| id | Имя | Роль | Аналог RA2 | Способность |
|---|---|---|---|---|
| alliance_construction_yard | Command Yard | двор, из MCV | Construction Yard | работает |
| alliance_power_plant | Power Station | энергия +200 | Power Plant | работает |
| alliance_barracks | Training Camp | пехота | Barracks | работает |
| alliance_ore_refinery | Ore Processor | ОПЗ, харвестер | Ore Refinery | работает |
| alliance_war_factory | Vehicle Plant | техника | War Factory | работает |
| alliance_air_force_command | Air Command | радар; авиация | Airforce Command HQ | радар работает; авиация позже (M11b) |
| alliance_naval_shipyard | Harbor | верфь | Naval Shipyard | работает: стоит на глубокой воде, строит корабли (этап 2а); ремонт кораблей позже |
| alliance_service_depot | Repair Bay | ремонт техники | Service Depot | позже |
| alliance_battle_lab | Research Lab | высокий тех | Battle Lab | работает |
| alliance_ore_purifier | Ore Refining Annex | +доход с руды | Ore Purifier | позже |
| alliance_spy_satellite_uplink | Orbital Uplink | открывает карту | Spy Satellite Uplink | радар работает; карта позже |
| alliance_robot_control_center | Drone Control Hub | управление роботами | Robot Control Center | позже |
| alliance_chronosphere | Phase Gate | переброска группы | Chronosphere | позже (M11c) |
| alliance_weather_control_device | Storm Generator | шторм над областью | Weather Control Device | позже (M11c) |
| alliance_gap_generator | Veil Generator | скрывает базу | Gap Generator | позже (M11c) |
| alliance_pillbox | Gun Nest | пулемёт | Pillbox | работает |
| alliance_patriot_missile_system | Sky Guard | ПВО | Patriot Missile System | работает (по авиации — M11b) |
| alliance_prism_tower | Lens Tower | лучевая оборона | Prism Tower | работает; цепочки башен позже (M11c) |
| alliance_grand_cannon | Siege Battery | дальняя артиллерия | Grand Cannon | работает |
| alliance_wall | Barrier | стены | Fortress Wall | работает |

**Юниты Альянса** (остальная армия — M11c):

| id | Имя | Роль | Аналог RA2 |
|---|---|---|---|
| alliance_mcv | Mobile Command Unit | MCV | Allied MCV |
| alliance_harvester | Ore Hauler | харвестер, без оружия | Chrono Miner (без телепорта) |
| alliance_rifleman | Rifleman | пехота, стартовая | GI |
| alliance_light_tank | Light Tank | основной танк | Grizzly Battle Tank |

**Оружие Альянса:** Assault Rifle, 105mm Cannon, Heavy Machine Gun, Lens Beam, Siege Shell, Sky Guard Missile.


## Поздние юниты (этап 1 наполнения, 2026-10-24)

Все юниты черновиков `docs/ra2_units/yaml_drafts` — у всей фракции, включая юнитов отдельных стран RA2 (раздел по странам — M11). Оружие и способности — этап 3 (`ability_later`); корабли и авиация строятся с этапа 2, когда появятся верфь на воде и полёт (`available_later`). Navy SEAL не взят: в скирмише RA2 его не строят. Черновик `desolator` описывал ракетную установку — она внесена как `rocket_artillery`, а `desolator` — пехотинец.

### Союз

| id | Имя | Роль | Аналог RA2 | Сейчас |
|---|---|---|---|---|
| attack_dog | War Hound | attack dog: bites infantry, M11 weapon | Attack Dog | строится, без оружия — этап 3 |
| tesla_trooper | Shock Infantry | electric infantry | Shock Trooper | строится, без оружия — этап 3 |
| crazy_ivan | Bomb Specialist | plants bombs | Crazy Ivan | строится, без оружия — этап 3 |
| terrorist | Demolition Trooper | blows up with its target | Terrorist | строится, без оружия — этап 3 |
| desolator | Toxin Trooper | poisons the ground | Desolator | строится, без оружия — этап 3 |
| rocket_artillery | Rocket Artillery | long-range rocket (the draft listed it as desolator) | V3 Rocket Launcher | строится, без оружия — этап 3 |
| apocalypse_tank | Super Heavy Tank | twin cannons and missiles | Apocalypse Tank | строится, без оружия — этап 3 |
| terror_drone | Combat Drone | climbs into vehicles | Terror Drone | строится, без оружия — этап 3 |
| demolition_truck | Demolition Truck | drives into the target and blows up | Demolition Truck | строится, без оружия — этап 3 |
| kirov_airship | Heavy Airship | bomber airship | Kirov Airship | в сайдбаре, строится с этапа 2 (полёт) |
| hover_transport | Hovercraft | carries units over water | Hover Transport | строится верфью (этап 2а), без оружия — этап 3 |
| typhoon_sub | Attack Submarine | torpedoes ships | Typhoon Attack Sub | строится верфью (этап 2а), без оружия — этап 3 |
| sea_scorpion | Anti-Air Hydrofoil | anti-air boat | Sea Scorpion | строится верфью (этап 2а), без оружия — этап 3 |
| giant_squid | Trained Squid | grabs ships | Giant Squid | строится верфью (этап 2а), без оружия — этап 3 |
| dreadnought | Missile Battleship | long-range missiles | Dreadnought | строится верфью (этап 2а), без оружия — этап 3 |

### Альянс

| id | Имя | Роль | Аналог RA2 | Сейчас |
|---|---|---|---|---|
| alliance_engineer | Field Engineer | captures and repairs buildings | Engineer | строится, без оружия — этап 3 |
| alliance_attack_dog | Guard Dog | attack dog | Attack Dog | строится, без оружия — этап 3 |
| alliance_rocketeer | Jetpack Trooper | flies over the field (M11b) | Rocketeer | строится, без оружия — этап 3 |
| alliance_spy | Infiltrator | disguises and enters enemy buildings | Spy | строится, без оружия — этап 3 |
| alliance_chrono_legionnaire | Time Trooper | erases targets from time | Chrono Legionnaire | строится, без оружия — этап 3 |
| alliance_tanya | Elite Commando | hero: kills infantry, bombs buildings | Tanya | строится, без оружия — этап 3 |
| alliance_sniper | Marksman | long-range anti-infantry | Sniper | строится, без оружия — этап 3 |
| alliance_ifv | Infantry Fighting Vehicle | weapon by its passenger | IFV | строится, без оружия — этап 3 |
| alliance_prism_tank | Beam Tank | splitting beam | Prism Tank | строится, без оружия — этап 3 |
| alliance_mirage_tank | Holo Tank | disguised as a tree | Mirage Tank | строится, без оружия — этап 3 |
| alliance_tank_destroyer | Tank Hunter | anti-tank gun | Tank Destroyer | строится, без оружия — этап 3 |
| alliance_nighthawk | Transport Helicopter | carries infantry | Night Hawk Transport | в сайдбаре, строится с этапа 2 (полёт) |
| alliance_harrier | Strike Jet | strikes and returns to rearm | Harrier | в сайдбаре, строится с этапа 2 (полёт) |
| alliance_black_eagle | Heavy Strike Jet | heavier strike jet | Black Eagle | в сайдбаре, строится с этапа 2 (полёт) |
| alliance_amphibious_transport | Landing Craft | carries units over water | Amphibious Transport | строится верфью (этап 2а), без оружия — этап 3 |
| alliance_dolphin | Sonar Dolphin | sonic attack | Dolphin | строится верфью (этап 2а), без оружия — этап 3 |
| alliance_destroyer | Destroyer | guns and anti-sub | Destroyer | строится верфью (этап 2а), без оружия — этап 3 |
| alliance_aegis_cruiser | Air Defense Cruiser | anti-air missiles | Aegis Cruiser | строится верфью (этап 2а), без оружия — этап 3 |
| alliance_aircraft_carrier | Aircraft Carrier | launches its own jets | Aircraft Carrier | строится верфью (этап 2а), без оружия — этап 3 |
