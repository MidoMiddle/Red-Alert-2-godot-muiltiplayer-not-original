# Импорт карт Red Alert 2

Скрипты переводят карту RA2 / Yuri's Revenge (`.mpr`, `.yrm`) в наш формат `.rtsmap` ([формат](../../docs/formats/rtsmap.md)). Нужен Python 3, сторонние пакеты не нужны. Файлы RA2 в репозиторий не кладём: архив карт лежит у владельца и игнорируется git.

1. `ra2_unpack.py <карта> <папка>` распаковывает сжатые секции:
   - `IsoMapPack5` (LZO1X): тайлы и высоты клеток → `iso.json`;
   - `OverlayPack` и `OverlayDataPack` (формат 80 Westwood): руда, самоцветы, мосты → `overlay.bin`, `overlaydata.bin`;
   - `PreviewPack`: превью → `preview.ppm`;
   - обычные секции → `sections.json`.
2. `ra2_to_rtsmap.py <папка> <out.rtsmap> --id … --name … --author … --description … [--climate temperate] [--lighting day]` собирает карту. Как именно — в начале скрипта:
   - **Земля:** каждый номер тайла получает вид по тому цвету, который его клетки чаще всего имеют на превью этой карты: вода, дорога, песок, грунт, тёмная или светлая трава.
   - **Высоты:** угол клетки — наибольший уровень соседних клеток. Под зданиями земля выровнена.
   - **Ресурсы и мосты:** руда, самоцветы, низкие и высокие мосты (у нас мост в одну клетку по середине).
   - **Деревья и бурильщики:** обычные деревья переносятся. У каждого старта ставятся бурильщик самоцветов (на ближайшем рудном дереве) и бурильщик руды, остальные рудные деревья пропускаются.
   - **Здания** — по таблицам `STRUCTURES` и `OBJECTS`. Чему нет пары, то пропускается, и скрипт это перечисляет.

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
   ```
3. `render_rtsmap.py <карта.rtsmap> <out.ppm>` рисует карту в осях экрана, как превью RA2: так её удобно сравнить с оригиналом.

После сборки: `dotnet run --project tools/Rts.MapTool -- canon <карта>`, затем `... -- check <карта>`.
