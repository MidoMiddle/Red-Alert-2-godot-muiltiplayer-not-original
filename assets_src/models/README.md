# Модели: исходники

> Статус: действует · Обновлён: 2026-10-28 · Создан: 2026-10-23

Исходники моделей в Blender (`.blend`) и сырые генерации. Готовые модели для игры лежат в `game/assets/models/`. Оба каталога в Git LFS (см. `.gitattributes`).

## Раскладка

- `assets_src/models/<категория>/<id>/<id>.blend` — исходник, один файл на элемент.
- `assets_src/models/<категория>/<id>/bake/<id>_albedo.png`, `<id>_normal.png`, `<id>_orm.png` — запечённые текстуры. Из них собирается материал в `.glb`; в игру сами PNG не идут.
- `game/assets/models/<категория>/<id>/<id>.glb` — экспорт для Godot, текстуры внутри файла. Вариант объекта (повреждённый, занятый, руины) — своя папка со своим id, как `building_house_small_damaged`.
- Сырые генерации Meshy и Tripo — `assets_src/generated/<id>/<сервис>/`, как сказано в [генерации моделей](../../docs/map_elements/generation_requirements.md).

Модель, сделанную скриптом, строит скрипт Blender `tools/blender/<units|buildings|families>/<id>.py` (образец: [tesla_coil.py](../../tools/blender/buildings/tesla_coil.py)). Он пишет `.blend`, `bake/` и `.glb` по путям выше. Правки вносим в скрипт и запускаем его заново: ручная правка `.blend` пропадёт при следующей сборке.

Пример: `assets_src/models/building/tesla_coil/tesla_coil.blend` → `game/assets/models/building/tesla_coil/tesla_coil.glb`.

Исключение — ёж: `assets_src/models/obstacle/anti_tank_hedgehog/anti_tank_hedgehog.blend` → `game/assets/models/obstacle/anti_tank_hedgehog/obstacle_anti_tank_hedgehog.glb`. Префикс категории в его имени оставляем, переименовывать готовый файл не будем.

## Правила

- 1 клетка = 1 единица Godot по X и Z. Масштаб объекта 1, поворот 0: трансформации применены.
- Оси Godot: +Y вверх, «перёд» смотрит в −Z. Blender работает в Z-up; экспорт glTF делаем с «+Y вверх».
- Точка опоры объекта на одной клетке — центр клетки, низ на земле.
- Бюджеты полигонов (LOD0): мелкий объект до 1 000 (предложение), средний до 3 000, техника до 8 000, здание до 20 000, пехота до 1 500.
- Материалы PBR: albedo, normal, ORM. Свечение (фонари, окна) делаем в Godot, не в модели.
- Импорт GLB в Godot: рядом с каждым `.glb` лежит `.glb.import` с `gltf/embedded_image_handling=2` («Embed as Basis Universal»). Текстуры остаются внутри `.glb`, и Godot не выносит PNG рядом с моделью. Значение 1 (по умолчанию) выносит их. Значение 3 не даёт mip-уровней, и на расстоянии текстуры мерцают, поэтому новый `.glb` импортируем с настройкой 2 и коммитим его `.glb.import` вместе с файлом.
- Каждый файл вносится строкой в [реестр ассетов](../../docs/assets_registry.md).

## Проверка

Скрипт `rts_validate.py` (масштаб, оси, имена, бюджет, материалы) ещё не написан. Пока проверка ручная по этому списку.
