## Набор пляж (beach_elements)

| Папка | Элемент | Статус | Блокер | Будущий файл | Заметка |
|---|---|---|---|---|---|
| 01_sand_dunes | Песок и дюны | в коде (тип земли) | — | — | тип `beach` (b) уже в terrain.yaml, скорости 90/80/70 совпадают; дюны — рельеф, записи нет |
| 02_palm | Пальма | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `palm`; как pine_tree в коде (400, пень); всех наземных |
| 03_driftwood | Коряга и прибрежный мусор | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `driftwood` (коряга, блокирует) и `driftwood_debris` (мусор, не блокирует; id производный); «как валун» — расхождение |
| 04_beach_umbrella | Пляжный зонтик | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `beach_umbrella`; не блокирует, прочность низкая |
| 05_sun_lounger | Шезлонг | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `sun_lounger`; не блокирует, прочность низкая |
| 06_beach_cabin | Пляжная кабинка | ждёт M11e | M11e | game/data/neutral/buildings.yaml | `neutral_beach_cabin`; 1×1, hp 800, гарнизона нет |
| 07_beach_shower | Пляжный душ | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `beach_shower`; не блокирует, прочность низкая; горение не указано |
| 08_beach_bar | Пляжный бар | ждёт M11e | M11e | game/data/neutral/buildings.yaml | `neutral_beach_bar`; 3×2, hp 800; дохода нет «как у магазина» (пункт 4) |
| 09_volleyball_net | Волейбольная сетка | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `volleyball_net`; не блокирует, прочность низкая |
| 10_beachgoers | Отдыхающие | нет записи | — | — | житель, в схеме его нет; данные по образцу 13_civilians (там другой путь файла) |
| 11_lifeguard_tower | Вышка спасателей | ждёт M11e, ждёт M11a | M11e; M11a | game/data/neutral/buildings.yaml | `neutral_lifeguard_tower`; 2×2, гарнизон 2 (предварительно) |
| 12_lighthouse | Маяк | ждёт M11e, ждёт M11a, ждёт M11b | M11e; M11a; M11b | game/data/neutral/buildings.yaml | `neutral_lighthouse`; 2×2, hp 1500, гарнизон 4 (предварительно); навигация флота — открытый вопрос |
| 13_buoy | Буй | ждёт M11e, ждёт M11b | M11e; M11b | game/data/climates/beach/map_objects.yaml | `buoy`; не блокирует, прочность низкая; навигационная роль — открытый вопрос |
| 14_pier | Пирс | ждёт M11e, ждёт M11b | M11e; M11b; segment_hp | — | записи нет: мост над водой, но segment_hp в документе не задан; логика как у bridge_wood_low (600 в коде) |
| 15_slipway | Слип | ждёт M11e, ждёт M11b | M11e; M11b | game/data/climates/beach/map_objects.yaml | `slipway`; hp 1500, не блокирует; высадка амфибий (M11b) |
| 16_breakwater | Волнорез | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `breakwater`; блокирует всех, в том числе флот; связи с M11b в документе нет |
| 17_beached_boat | Лодка на песке | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `beached_boat`; одна клетка; остаётся обломок, клетка проходима |
| 18_boat_shed | Сарай для лодок | ждёт M11e | M11e | game/data/neutral/buildings.yaml | `neutral_boat_shed`; 2×2, гарнизона нет |
| 19_fishing_nets | Рыбацкие сети | ждёт M11e | M11e | game/data/climates/beach/map_objects.yaml | `fishing_nets`; не блокирует, прочность низкая; в тексте «механики нет» — расхождение |
