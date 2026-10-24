# Имена в игре: рабочий список

Единый список «id в данных → имя на экране». Имена — свои, английские, **рабочие**: финальные выбирает владелец позже, вместе с названиями фракций и игры (M11, раздел 9; [15_legal.md](../15_legal.md) — имена EA в игру не попадают). Аналог RA2 — для справки разработчикам. **Временно (решение владельца 2026-10-24) интерфейс показывает имена RA2**, пока в игре нет своих моделей и иконок: в сайдбаре, подсказках и кнопках суперорудий. Код и данные держат свои имена; имена RA2 — ключи `RA2_NAME_<ID>` в `game/localization/en.csv`, вывод — `DisplayNames`. Выключается в настройках: «Game» → «Names of Red Alert 2». Перед выпуском убрать (см. [15_legal.md](../15_legal.md)).

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
| flak_cannon | Flak Cannon | ПВО | Flak Cannon | работает: бьёт по авиации (этап 2б) |
| naval_shipyard | Dockyard | верфь | Naval Shipyard | работает: стоит на глубокой воде, строит корабли (этап 2а); ремонт кораблей позже |
| service_depot | Field Workshop | ремонт техники | Service Depot | работает: чинит технику и корабли рядом, за деньги (этап 2в) |
| nuclear_reactor | Atomic Plant | энергия +2000 | Nuclear Reactor | работает: энергия; взрывается, если его разрушили выстрелом (этап 2в) |
| iron_curtain | Bastion Field Projector | неуязвимость группе | Iron Curtain | работает: 3×3 клетки неуязвимы 50 с, пехота там гибнет; заряд 5 мин (этап 2д) |
| nuclear_missile_silo | Strategic Missile Silo | ракетный удар | Nuclear Missile Silo | работает: ракета 600 на 10 клеток, бетон 8 %, после — яд; заряд 10 мин (этап 2д) |
| industrial_plant | Heavy Industry Complex | скидка на технику | Industrial Plant | работает: техника, корабли и самолёты на 25 % дешевле (этап 2в) |
| battle_bunker | Fortified Bunker | гарнизон | Battle Bunker | работает: до 5 пехотинцев внутри стреляют своим оружием, дальность от стены (этап 2е) |

Имена первых одиннадцати пришли из M3 и тоже рабочие: «Tesla Coil», «War Factory», «Flak Cannon» совпадают с RA2 и заменяются вместе с остальными.

## Альянс (`alliance`)

| id | Имя | Роль | Аналог RA2 | Способность |
|---|---|---|---|---|
| alliance_construction_yard | Command Yard | двор, из MCV | Construction Yard | работает |
| alliance_power_plant | Power Station | энергия +200 | Power Plant | работает |
| alliance_barracks | Training Camp | пехота | Barracks | работает |
| alliance_ore_refinery | Ore Processor | ОПЗ, харвестер | Ore Refinery | работает |
| alliance_war_factory | Vehicle Plant | техника | War Factory | работает |
| alliance_air_force_command | Air Command | радар; авиация | Airforce Command HQ | работает: радар и производство авиации (этап 2б) |
| alliance_naval_shipyard | Harbor | верфь | Naval Shipyard | работает: стоит на глубокой воде, строит корабли (этап 2а); ремонт кораблей позже |
| alliance_service_depot | Repair Bay | ремонт техники | Service Depot | работает: чинит технику и корабли рядом, за деньги (этап 2в) |
| alliance_battle_lab | Research Lab | высокий тех | Battle Lab | работает |
| alliance_ore_purifier | Ore Refining Annex | +доход с руды | Ore Purifier | работает: руда приносит на 25 % больше (этап 2в) |
| alliance_spy_satellite_uplink | Orbital Uplink | открывает карту | Spy Satellite Uplink | работает: радар; пока стоит с энергией, вся карта разведана (этап 2г) |
| alliance_robot_control_center | Drone Control Hub | управление роботами | Robot Control Center | работает: пока стоит с энергией, Robot Tank работают (этап 3г) |
| alliance_chronosphere | Phase Gate | переброска группы | Chronosphere | работает: переносит технику и корабли 3×3 в другое место, пехота гибнет; заряд 7 мин (этап 2д) |
| alliance_weather_control_device | Storm Generator | шторм над областью | Weather Control Device | работает: через ~17 с буря на 12 с, молнии по 250; заряд 10 мин (этап 2д) |
| alliance_gap_generator | Veil Generator | скрывает базу | Gap Generator | работает: с энергией закрывает 10 клеток вокруг от врага (этап 2г) |
| alliance_pillbox | Gun Nest | пулемёт | Pillbox | работает |
| alliance_patriot_missile_system | Sky Guard | ПВО | Patriot Missile System | работает: бьёт по авиации (этап 2б) |
| alliance_prism_tower | Lens Tower | лучевая оборона | Prism Tower | работает; свободные башни рядом усиливают выстрел, +50 % каждая, до 8 (этап 2е) |
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
| attack_dog | War Hound | attack dog: bites infantry, M11 weapon | Attack Dog | работает: укус убивает любого пехотинца, технику не трогает (этап 3а) |
| tesla_trooper | Shock Infantry | electric infantry | Shock Trooper | работает: электроразряд, дальность 3 (этап 3а) |
| crazy_ivan | Bomb Specialist | plants bombs | Crazy Ivan | работает: вешает бомбу, через 30 с взрыв 400 по площади; гибель носителя взрывает сразу (этап 3б) |
| terrorist | Demolition Trooper | blows up with its target | Terrorist | работает: сам целей не ищет; по приказу подбегает и взрывается, взрывается и убитым (этап 3б) |
| desolator | Toxin Trooper | poisons the ground | Desolator | работает: ядовитый луч против пехоты, здания не трогает (этап 3а); развёрнутый (D) заражает землю ядом, яд ранит всех на земле, кроме него (этап 3б) |
| rocket_artillery | Rocket Artillery | long-range rocket (the draft listed it as desolator) | V3 Rocket Launcher | работает: ракета на 5–18 клеток, только по приказу; ПВО может её сбить (этап 3в); от слишком близкой цели отъезжает |
| apocalypse_tank | Super Heavy Tank | twin cannons and missiles | Apocalypse Tank | работает: две пушки по земле, ракеты только по воздуху (этап 3в); лечится сам, 2 HP в секунду |
| terror_drone | Combat Drone | climbs into vehicles | Terror Drone | работает: запрыгивает в технику и ест её изнутри по 50 раз в 4 с, пехоту убивает сразу; мастерская его вытаскивает (этап 3г) |
| demolition_truck | Demolition Truck | drives into the target and blows up | Demolition Truck | работает: только по приказу подъезжает и взрывается (300), взрывается и подбитым, оставляет немного яда (этап 3г) |
| kirov_airship | Heavy Airship | bomber airship | Kirov Airship | работает: бомбы 250 по площади 2 клетки; сбитый падает на тех, кто под ним (этап 3е) |
| hover_transport | Hovercraft | carries units over water | Hover Transport | работает: 12 слотов (пехота 1, техника 3, MCV и Super Heavy Tank 6), по воде и суше; высадка — D (этап 3г) |
| typhoon_sub | Attack Submarine | torpedoes ships | Typhoon Attack Sub | работает: под водой, видна только сенсорам; торпеды по кораблям и подводным (этап 3д) |
| sea_scorpion | Anti-Air Hydrofoil | anti-air boat | Sea Scorpion | работает: флак по воде и берегу, зенитки по воздуху (этап 3д) |
| giant_squid | Trained Squid | grabs ships | Giant Squid | работает: под водой; хватает корабль, держит и давит; щупальцем бьёт подводных (этап 3д) |
| dreadnought | Missile Battleship | long-range missiles | Dreadnought | работает: ракеты на 8–25 клеток по приказу, ПВО может их сбить (этап 3д) |

### Альянс

| id | Имя | Роль | Аналог RA2 | Сейчас |
|---|---|---|---|---|
| alliance_engineer | Field Engineer | captures and repairs buildings | Engineer | работает: захватывает здание врага (не оборону, верфи, супероружие), чинит своё целиком (этап 3б) |
| alliance_attack_dog | Guard Dog | attack dog | Attack Dog | работает: укус убивает любого пехотинца, технику не трогает (этап 3а) |
| alliance_rocketeer | Jetpack Trooper | flies over the field (M11b) | Rocketeer | работает: летает, бьёт по земле и воздуху; достать его может только ПВО (этап 3а) |
| alliance_spy | Infiltrator | disguises and enters enemy buildings | Spy | работает: враг видит его своим пехотинцем, узнают только собаки; в электростанции — отключение энергии, в радаре — сброс карты, в ОПЗ — половина денег (этап 3б) |
| alliance_chrono_legionnaire | Time Trooper | erases targets from time | Chrono Legionnaire | работает: луч замораживает и стирает цель, прерванный — цель цела (этап 3а); прыгает туда, куда послан, и проявляется (этап 3б) |
| alliance_tanya | Elite Commando | hero: kills infantry, bombs buildings | Tanya | работает: пистолеты убивают пехотинца с выстрела (этап 3а); C4 сносит здание целиком за 2 с (этап 3б) |
| alliance_sniper | Marksman | long-range anti-infantry | Sniper | работает: винтовка на 14 клеток против пехоты (этап 3а) |
| alliance_ifv | Infantry Fighting Vehicle | weapon by its passenger | IFV | работает: пустой — ракеты по земле и воздуху; с пехотинцем — его оружие по таблице RA2 (этап 3г); с инженером — ремонтный кран: чинит свою технику и вытаскивает дрона |
| alliance_prism_tank | Beam Tank | splitting beam | Prism Tank | работает: луч 100, до 5 осколков по 30 в радиусе 3 клеток, здания — вдвое (этап 3в) |
| alliance_mirage_tank | Holo Tank | disguised as a tree | Mirage Tank | работает: стоя на месте, для врага — дерево, его не выбирают целью; сам за целью не едет (этап 3в) |
| alliance_tank_destroyer | Tank Hunter | anti-tank gun | Tank Destroyer | работает: подкалиберная пушка 150, полный урон по лёгкой и тяжёлой технике, по пехоте 2 % (этап 3в) |
| alliance_nighthawk | Transport Helicopter | carries infantry | Night Hawk Transport | работает: везёт 5 пехотинцев (техника не помещается), пулемёт по земле по приказу (этап 3е) |
| alliance_harrier | Strike Jet | strikes and returns to rearm | Harrier | работает: по приказу залп 2 × 150, затем летит к командованию ВВС и перезаряжается 18 с (этап 3е); не больше 4 штурмовиков на командование |
| alliance_black_eagle | Heavy Strike Jet | heavier strike jet | Black Eagle | работает: по приказу залп 2 × 200, затем перезарядка у командования ВВС (этап 3е) |
| alliance_amphibious_transport | Landing Craft | carries units over water | Amphibious Transport | работает: 12 слотов, по воде и суше; высадка — D (этап 3г) |
| alliance_robot_tank | Robot Tank | works only with the hub | Robot Tank | работает: пушка; без Drone Control Hub с энергией стоит и не стреляет; числа свои (этап 3г) |
| alliance_hornet | Hornet | the carrier's jet | Hornet | работает: не строится, его выпускает авианосец; один залп и обратно на палубу |
| alliance_dolphin | Sonar Dolphin | sonic attack | Dolphin | работает: под водой, сенсор 4 клетки; звуковая волна по кораблям и подводным (этап 3д); освобождает свой корабль от кальмара |
| alliance_destroyer | Destroyer | guns and anti-sub | Destroyer | работает: пушка по воде и берегу, глубинные бомбы под воду, сенсор 8 клеток (этап 3д) |
| alliance_aegis_cruiser | Air Defense Cruiser | anti-air missiles | Aegis Cruiser | работает: ракеты только по воздуху, сбивает и ракеты в полёте (этап 3д) |
| alliance_aircraft_carrier | Aircraft Carrier | launches its own jets | Aircraft Carrier | работает: по приказу выпускает три «Хорнета» — настоящих самолёта: бьют и садятся обратно, потерянный восстанавливается за 27 с |
