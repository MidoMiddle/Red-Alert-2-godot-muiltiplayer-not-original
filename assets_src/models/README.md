# Модели: исходники

> Статус: первая пробная модель (ёж) · Создан: 2026-10-23

Исходники моделей в Blender (`.blend`) и сырые генерации. Готовые модели для игры лежат в `game/assets/models/`. Оба каталога в Git LFS (см. `.gitattributes`).

## Раскладка

- `assets_src/models/<категория>/<id>/<id>.blend` — исходник, один файл на элемент.
- `game/assets/models/<категория>/<id>/<категория>_<id>[_вариант][_damaged].glb` — экспорт для Godot.
- Сырые генерации Meshy и Tripo — `assets_src/generated/<id>/<сервис>/`, как сказано в [генерации моделей](../../docs/map_elements/generation_requirements.md).

Пример: `assets_src/models/obstacle/anti_tank_hedgehog/anti_tank_hedgehog.blend` → `game/assets/models/obstacle/anti_tank_hedgehog/obstacle_anti_tank_hedgehog.glb`.

## Правила

- 1 клетка = 1 единица Godot по X и Z. Масштаб объекта 1, поворот 0: трансформации применены.
- Оси Godot: +Y вверх, «перёд» смотрит в −Z. Blender работает в Z-up; экспорт glTF делаем с «+Y вверх».
- Точка опоры объекта на одной клетке — центр клетки, низ на земле.
- Бюджеты полигонов (LOD0): мелкий объект до 1 000 (предложение), средний до 3 000, техника до 8 000, здание до 20 000, пехота до 1 500.
- Материалы PBR: albedo, normal, ORM. Свечение (фонари, окна) делаем в Godot, не в модели.
- Каждый файл вносится строкой в [реестр ассетов](../../docs/assets_registry.md).

## Проверка

Скрипт `rts_validate.py` (масштаб, оси, имена, бюджет, материалы) ещё не написан. Пока проверка ручная по этому списку.
