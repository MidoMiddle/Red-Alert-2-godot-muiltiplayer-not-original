# Импорт карт Red Alert 2

Скрипты переводят карту RA2 / Yuri's Revenge (`.mpr`, `.yrm`) в наш формат `.rtsmap` ([формат](../../docs/formats/rtsmap.md)). Нужен Python 3, сторонние пакеты не нужны. Файлы RA2 в репозиторий не кладём: архив карт лежит у владельца и игнорируется git.

1. `ra2_unpack.py <карта> <папка>` распаковывает сжатые секции:
   - `IsoMapPack5` (LZO1X): тайлы и высоты клеток → `iso.json`;
   - `OverlayPack` и `OverlayDataPack` (формат 80 Westwood): руда, самоцветы, мосты → `overlay.bin`, `overlaydata.bin`;
   - `PreviewPack`: превью → `preview.ppm`;
   - обычные секции → `sections.json`.
2. `ra2_to_rtsmap.py <папка> <out.rtsmap> --id … --name … --author … --description … [--climate temperate] [--lighting day] [--players N] [--raised-land]` собирает карту. Как именно — в начале скрипта:
   - **Земля:** каждый номер тайла получает вид по тому цвету, который его клетки чаще всего имеют на превью этой карты: вода, дорога, песок, грунт, тёмная или светлая трава. Превью, нарисованное вдвое уже карты, читается с масштабом. Плоские цвета редактора (Route 66) тоже узнаются: оранжевый — грунтовая дорога, жёлтый — песок. В городе (`--climate urban`) красно-коричневые тротуары RA2 становятся брусчаткой (`pavers`, проходимы как дорога), а тёмная трава — обычной: поля в городском климате нет.
   - **Высоты:** угол клетки — наибольший уровень соседних клеток. Под зданиями земля выровнена.
   - **Ресурсы:** руда и самоцветы.
   - **Мосты:** низкий мост RA2 — `bridge_wood_low` над водой по средней линии. Высокий мост — `bridge_concrete_high` шириной 3 над тем, что под ним в RA2 (долина, речка). Его концы продлеваются через обрывы до ровных берегов. Мост RA2 шириной в клетку часто упирается в «язык» плато такой же ширины: клетки берега на боковых полосах поднимаются до его уровня, получается площадка перед мостом. Одиночная клетка опоры между двумя пролётами RA2 входит в мост и опускается до земли рядом: опоры рисует сам мост (эстакады The Light of Night).
   - **Деревья и бурильщики:** обычные деревья переносятся, рудные деревья пропускаются. У каждого старта ставится бурильщик руды. Бурильщиков самоцветов у стартов нет: их ставит `share-gems` (ниже).
   - **Стены** (оверлей 240) — каменные стены `wall_stone`.
   - **Старты** — путевые точки от 0 до `MaxPlayer` − 1 из `[Basic]`; если `MaxPlayer` нет — до `--players` − 1 (по умолчанию 4).
   - **`--raised-land`:** приподнятая клетка не бывает водой (вода в RA2 всегда на уровне 0), она берёт самый частый не-водный вид своего тайла. Нужен Route 66, где прибрежные обрывы на превью синие. Прежние карты собраны без него: с ним у них часть береговых обрывов станет сушей.
   - **Игровое поле** — `LocalSize`, верх поднят на две его единицы. Если старт ближе 4 клеток к краю, поле расширяется, чтобы база помещалась.
   - **Фонари** — деревья карты `LT_GEN*` становятся фонарными столбами.
   - **Здания** — по таблицам `STRUCTURES` и `OBJECTS`. Чему нет пары, что стоит на мосту или на клетках другого здания, то пропускается, и скрипт это перечисляет. Размеры зданий скрипт берёт из `game/data` (`footprint`, у записи без него — по `inherits`).
   - **Постройки сторон** у нейтрального хозяина (оборона, супероружие, АЭС, стройплощадки, казармы, завод, лаборатория, командование ВВС, клон-чаны) переносятся как есть, для «никого»: оборона стреляет по всем игрокам, инженер захватывает то, что разрешают данные. Бункеры `CABUNK01`/`CABUNK02` становятся `neutral_civilian_bunker` (8 бойцов). Ночные фонари `TEMNITLAMP` — фонарные столбы; фонарь под нашим зданием (наши пятна бывают больше, чем в RA2) убирается. Постройку не нейтрального хозяина скрипт ставит тоже для «никого» и предупреждает. `GAGATE_A` и `AMMOCRAT` пары не имеют.

   Собраны так:

   ```bash
   python3 tools/ra2_import/ra2_to_rtsmap.py <папка heaven> game/data/maps/heaven.rtsmap --id heaven --name "Heaven" \
     --author "Alien (Red Alert 2: Yuri's Revenge, 2004); converted" \
     --description "Four bases on high ground at the corners, a green valley in a cross between them, a tech island in a ring of water with four low bridges. From heaven.yrm."
   python3 tools/ra2_import/ra2_to_rtsmap.py <папка fc2> game/data/maps/facing-corners.rtsmap --id facing-corners --name "Facing Corners" \
     --author "flamincon (Red Alert 2, 2005); converted" \
     --description "Four islands on high ground facing each other across a cross of water, bridges over the channels, a tech island with an airfield, a lab and oil derricks in the middle. From facing corners 2 by flamincon.mpr."
   python3 tools/ra2_import/ra2_to_rtsmap.py <папка steph> game/data/maps/stephs-high-ground.rtsmap --id stephs-high-ground \
     --name "Steph's High Ground" --author "Stephane Gingras (Red Alert 2, 2001); converted" \
     --description "Four bases on their own high plateaus, a river from west to east with two bridges, a town in the middle and four oil derricks to take. From Steph's High Ground.mpr."
   python3 tools/ra2_import/ra2_to_rtsmap.py <папка warpro> game/data/maps/war-pro.rtsmap --id war-pro --name "WAR Pro" \
     --author "Bdizzle18 (Red Alert 2, 2005); converted" \
     --description "Eight bases round a great field of gems that spells WAR PRO, rivers between them, and in the middle a walled island with an airfield and bridge repair huts, reached by four bridges; oil derricks and tech outposts to take. From WARPRO2.mpr."
   python3 tools/ra2_import/ra2_to_rtsmap.py <папка lon> game/data/maps/light-of-night.rtsmap --id light-of-night --name "The Light of Night" \
     --author "tankcmdr6 (Red Alert 2, 2001); converted" --climate urban --lighting night \
     --description "A town at night for eight: two elevated highways on concrete spans run across it over the streets, lit roads and brick pavements between the blocks, a seaport on the bay, gold by every base and many oil derricks to take. From The Light Of Night II (#2lightonight.mpr)."
   python3 tools/ra2_import/ra2_to_rtsmap.py <папка r66> game/data/maps/route-66.rtsmap --id route-66 --name "Route 66" \
     --author "Ejah (Red Alert 2, 2001); converted" --players 8 --raised-land \
     --description "Eight island bases strung on one long highway: high concrete bridges carry it from island to island, and between every two starts lies a neutral island with oil derricks, a tech airfield or a power plant. Ore and gem fields on every island. From Route 66 by Ejah.mpr."
   ```
3. `render_rtsmap.py <карта.rtsmap> <out.ppm>` рисует карту в осях экрана, как превью RA2: так её удобно сравнить с оригиналом.

После сборки: `dotnet run --project tools/Rts.MapTool -- share-gems <карта>`, затем `... -- canon <карта>` и `... -- check <карта>`.

**Опоры высоких мостов** — `ra2_piers.py <карта.rtsmap> ...`: опора RA2 в две клетки (Route 66) импортом не опускается и проходит сквозь полотно. Скрипт опускает под пролётом каждого высокого моста всё, что достаёт до уровня полотна, к самому низкому углу под пролётом (вода или долина).

**Виды дорог** ([04 Дороги](../../docs/map_elements/04_roads/README.md)) — `ra2_roads.py [--no-highway] [--dirt-road] <карта.rtsmap> ...`, после сборки, затем `canon` и `check`. Наборов тайлов RA2 у нас нет, поэтому вид дороги берётся по форме полосы асфальта:
- полоса шириной 3–6 клеток и длиной от 8 — шоссе (`H`, текстура `highway`): главные дороги карт RA2 и проспекты города;
- шире — площадь или стоянка, остаётся асфальтом;
- в 1–2 клетки в городе — улочка (текстура `street`), вне города остаётся асфальтом;
- `--dirt-road`: узкий асфальт рядом с грунтом — край грунтовки (`g`): грунтовка RA2 шириной в три клетки, на превью коричневая в середине и серая по краям;
- `--no-highway`: без шоссе;
- `--pave`: вся дорога — шоссе, и грунтовая, и асфальтовая. Route 66: к высоким бетонным мостам в три клетки грунтовка не ведёт (владелец, 2026-10-30).
Отбойников нет: в RA2 их нет. 2026-10-30 применено так:

```bash
python3 tools/ra2_import/ra2_roads.py game/data/maps/light-of-night.rtsmap game/data/maps/facing-corners.rtsmap game/data/maps/stephs-high-ground.rtsmap
python3 tools/ra2_import/ra2_piers.py game/data/maps/route-66.rtsmap
python3 tools/ra2_import/ra2_roads.py --no-highway --dirt-road game/data/maps/route-66.rtsmap
python3 tools/ra2_import/ra2_roads.py --pave game/data/maps/route-66.rtsmap
```

На Heaven и WAR Pro асфальтовых дорог нет.

На Facing Corners бурильщики руды у стартов стоят там, куда их поставил прежний импортёр: новый при равных расстояниях выбирает другую клетку. При повторном импорте эти три бурильщика (и клетки руды под ними) возвращены на свои места, чтобы экономика карты не менялась.

`share-gems` ставит бурильщиков самоцветов на общую территорию, дальше 35 клеток от любого старта. Если пути танков от всех стартов до одной клетки различаются не больше чем на 30 %, бурильщик один на карту. Иначе — по одному на пару соседних стартов, посередине между ними (так на Facing Corners, где центр танкам недоступен).
