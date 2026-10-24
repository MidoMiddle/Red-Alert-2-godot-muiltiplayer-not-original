> **2026-10-24:** все юниты Союза и Альянса из этих черновиков перенесены в игру (`game/data/factions/*/units_advanced.yaml`) со своими рабочими именами ([docs/content/names.md](../../content/names.md)), требования Альянса — на его здания; оружие — этап 3, корабли и авиация — этап 2. Navy SEAL не перенесён. Черновики Культа — по-прежнему черновики.

# Заготовки YAML для юнитов сверх MVP

> Статус: черновик · Создан: 2026-10-23
> Связанные документы: [справочник юнитов](../README.md), [очерёдность открытия](../unlock_order.md), [M11_content.md](../../implementation/M11_content.md), [M3_base_economy.md](../../implementation/M3_base_economy.md), раздел 4.6

Заготовки данных для 40 юнитов, которых нет в MVP. Формат тот же, что у `game/data/factions/union/units.yaml`. Игра эти файлы не читает: кода они не требуют и ничего не меняют.

---

## 1. Состав

| Файл | Фракция (рабочее имя) | Юнитов |
|---|---|---|
| [union/units.yaml](union/units.yaml) | Союз, юниты сверх MVP | 15 |
| [alliance/units.yaml](alliance/units.yaml) | Альянс | 24 |
| [culture/units.yaml](culture/units.yaml) | Культ (Юрий) | 1 |

Юниты MVP (8 штук из `game/data/factions/union/units.yaml`) сюда не входят. Это Призывник, Зенитчик (в YAML id `rocketeer`), советский инженер, советский MCV, War Harvester (`harvester`), Flak Track (`support_vehicle`), Rhino (`heavy_tank`) и Tesla Tank (`tesla_tank`).

Имена юнитов и фракций рабочие, финальные названия ещё не выбраны (открытый вопрос в [05_factions_content.md](../../05_factions_content.md)). Оригинальное имя RA2 стоит в комментарии у каждого юнита.

## 2. Что взято из правил RA2 1.006

| Поле | Откуда |
|---|---|
| `cost`, `hp`, `sight`, `armor`, `turret` | Поля `Cost`, `Strength`, `Sight`, `Armor`, `Turret` |
| `speed` | 6 × RA2 `Speed`, как в [templates.yaml](../../../game/data/shared/templates.yaml) |
| `turn_rate` | RA2 `ROT` × 2048 / 5, как в шаблоне |
| `build_time` | 1000 кредитов = 42 с, как в шаблоне |
| `prerequisites` | Поле `Prerequisite` и группы `Prerequisite*` из `[General]`, переведённые в id зданий |
| `locomotor` | GUID из поля `Locomotor`, переведённый в id из [locomotors.yaml](../../../game/data/shared/locomotors.yaml) |

Вложенный список в `prerequisites` значит «любое из перечисленных». Например, `[airforce_command, airforce_command_us]` — командование ВВС или штаб ВВС США.

## 3. Что ещё не решено (TODO)

- **`threat`** — очки угрозы для ИИ. В MVP они заданы, в RA2 их нет.
- **Оружие.** Имена оружия стоят в комментариях (`RA2 120mmx` и так далее). Сами id `<юнит>_primary` и `<юнит>_secondary` — заглушки, описывать их в `weapons.yaml` пока не нужно.
- **Типы передвижения.** `fly` нет в `locomotors.yaml`. Тип Хроно-харвестера (телепорт) не сверен, в файле так и помечено.
- **Базы.** `base_aircraft` и `base_ship` в шаблонах нет, поэтому у авиации и флота `inherits: TODO_...`.
- **Здания Альянса и Культа.** Id `airforce_command`, `airforce_command_us`, `naval_yard`, `service_depot` и `power_plant` временные. У Культа пока взяты здания Союза как заглушки.
- **Скорость флота и авиации.** Конверсия ×6 для них не проверена.
- **Страны.** Условия доступности (`RequiredHouses`, `ForbiddenHouses`) записаны комментарием, отдельного поля в YAML для них нет. Подробно — в [справочнике стран](../../ra2_countries/README.md).
- **Сложные механики.** Маскировка, телепорт, контроль разума, бомбы и радиация в YAML не описаны. Их пишут как трейты (M11, раздел 4.1).

## 4. Расхождение с RA2, которое уже есть в MVP

Flak Track у нас колёсный (`locomotor: wheel` в MVP, колёсная машина ПВО по [05_factions_content.md](../../05_factions_content.md)). В правилах RA2 у него гусеничный тип передвижения. Эта заготовка в MVP ничего не меняет, но при переносе в M11 это стоит проверить.

## 5. Как перенести в игру

1. Скопировать блоки в `game/data/factions/<фракция>/units.yaml` (для Альянса и Культа — новые папки).
2. Заменить TODO, описать здания и оружие.
3. Проверить валидатором `Rts.Data`.
4. Новый тип юнита сейчас требует строки в перечне `UnitType` в коде (порядок там часть формата команд и повторов), поэтому поднимается `SimulationVersion`. Так в M11 пока не сделано: по плану новый юнит с известными механиками должен быть только данными.

Файлы проверены YAML-парсером (Ruby): все три разбираются, всего 40 юнитов.
