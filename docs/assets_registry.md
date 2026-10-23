# Реестр ассетов

> Статус: создан · Дата: 2026-10-23
> Правила: [15_legal](15_legal.md), раздел 4; [conventions](implementation/conventions.md), раздел 8; [генерация моделей](map_elements/generation_requirements.md), раздел 10.

Каждый файл в `game/assets/` и каждая модель в `assets_src/` (кроме текстур-заглушек, см. их README) — отдельная строка. Для внешнего или сгенерированного ассета указываются источник, сервис и тариф. Для собственного — способ создания и лицензия «собственная работа проекта».

Колонки: **Файл** — путь в `game/assets/`; **Способ** — сервис или инструмент; **Тариф** — тариф сервиса или «—»; **Дата**; **Промт** — текст промта или «—»; **Референс** — документ, по которому сделан ассет; **Лицензия**; **Исходник** — путь в `assets_src/`.

Проверки в CI по правилу «ассет без записи — CI падает» пока нет: [conventions](implementation/conventions.md), раздел 8, относит её к M6. Строки вносятся вручную при добавлении файла.

## Записи

| Файл | Способ | Тариф | Дата | Промт | Референс | Лицензия | Исходник |
|---|---|---|---|---|---|---|---|
| `game/assets/models/obstacle/anti_tank_hedgehog/obstacle_anti_tank_hedgehog.glb` | Blender 5.2.3, вручную: геометрия и процедурный материал по [appearance.md](obstacle_elements/02_anti_tank_hedgehog/appearance.md), текстуры запечены 1024² (albedo, normal, ORM) | — | 2026-10-23 | — | [02 Противотанковый еж: как выглядит](obstacle_elements/02_anti_tank_hedgehog/appearance.md) | собственная работа проекта | `assets_src/models/obstacle/anti_tank_hedgehog/anti_tank_hedgehog.blend`; запечённые текстуры `assets_src/models/obstacle/anti_tank_hedgehog/bake/` |
