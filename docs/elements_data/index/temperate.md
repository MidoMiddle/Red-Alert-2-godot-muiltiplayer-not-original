## Набор умеренный (temperate_elements)

| Папка | Элемент | Статус | Блокер | Будущий файл | Заметка |
|---|---|---|---|---|---|
| 01_deciduous_tree | Лиственное дерево | ждёт M11e | да, всех наземных | — | вид pine_tree (drafts/map.yaml): та же логика, другая модель; запись не дублируется |
| 02_hedge | Живая изгородь | ждёт M11e | да, всех наземных | game/data/climates/temperate/map_objects.yaml | запись hedge; гусеницами не давится |
| 03_field | Поле | в коде (тип земли) | нет | — | тип «чисто» уже в коде; текстура — климат |
| 04_orchard | Фруктовый сад | ждёт M11e | да, каждое дерево в своей клетке | — | вид pine_tree (drafts/map.yaml), как у лиственного дерева; ряды — расстановка в файле карты |
| 05_hay_bale | Стог сена | ждёт M11e | да, всех наземных | game/data/climates/temperate/map_objects.yaml | запись hay_bale; горит очень быстро; после горения остаток, пень не назван |
| 06_well | Колодец | ждёт M11e | да, всех наземных | game/data/climates/temperate/map_objects.yaml | запись well; ресурсов и лечения нет |
| 07_water_tower | Водонапорная башня | ждёт M11e | основание, одна клетка; движители не указаны | game/data/climates/temperate/map_objects.yaml | запись water_tower; концепция относит башню к зданиям (расхождение документов) |
| 08_farmhouse | Фермерский дом | ждёт M11e, ждёт M11a | да, всех наземных | game/data/neutral/buildings.yaml | запись neutral_farmhouse; значения как у neutral_house_a (map.yaml), но горит; гарнизон 3 предварительно |
| 09_barn | Амбар | ждёт M11e | да, всех наземных | game/data/neutral/buildings.yaml | запись neutral_barn; 3x2; горит |
| 10_silo | Силос | ждёт M11e | да, всех наземных (одна клетка) | game/data/neutral/buildings.yaml | запись neutral_silo; значения как у neutral_smokestack (industrial.yaml): возможно один вид |
| 11_windmill | Ветряк | ждёт M11e | да, всех наземных | game/data/neutral/buildings.yaml | запись neutral_windmill; 2x2; горит |
| 12_farm_tractor | Трактор | нет записи, ждёт M11 | да, как техника | — | гражданская техника: вида в схеме нет, данные как у автомобилей |
| 13_livestock | Скот | нет записи, ждёт M11 | нет | — | житель: записи нет |
