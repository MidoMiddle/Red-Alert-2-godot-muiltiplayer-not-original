# Гражданские здания — промпты для генерации

> Фракция: нейтральное · Размер: 2×2 клеток (типовой дом, рабочее значение) · Роль: оборона (гарнизон пехотой)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Это не один объект, а набор разных гражданских зданий: жилые дома, офисы, магазины. Здания различаются по размеру и высоте; подтверждённых размеров по видам нет (appearance.md). Этот промпт описывает один типовой жилой дом 2×2 как образец, остальные здания набора делаются отдельно. В силуэте читается обычная городская застройка; занятость видна по вспышкам в окнах и пипам (UI), а не по форме здания.

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot (рабочее значение: в README размер не задан, параметры зависят от здания; 2×2 взято как типовой дом).
- **Высота:** два этажа, около полутора высот танка (предложение). У других зданий набора высота будет другой (не подтверждено).
- **Подвижные части (отдельные объекты):** одна часть и основание (в покое клипов нет, animations.md).
- **Узлы в Blender:** `light_*` на окнах по событию выстрела гарнизона (предложение); `damage_*` с пылью или дымом (предложение); клипы `open` и `close` на двери — только если в модели есть дверь и нужен выход бойцов (предложение).
- **Цвет команды:** не применяется: здание нейтральное, смены владельца нет (README; animations.md). Занятость показывают пипы (UI), а не маска на модели. В промпте зоны `#FF00FF` нет.
- **Палитра:** бетон и кирпич, без фракционных акцентов (гайд, раздел 4; предложение). По README дерево, сталь и бетон — это броня, зависящая от здания; внешний материал не подтверждён.
- **Особенности:**
  - Сохранить связку «занято: здание стреляет, при красной полоске бойцы выбегают» и показать её вспышками и пипами (appearance.md).
  - Сделать своим набор зданий и ориентиры, без привязки к памятникам (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A typical city house, a two-storey brick and concrete building on a flat rectangular base plate matching a 2×2 footprint. It is about one and a half times the height of a battle tank, with a flat or low pitched roof. Rows of plain rectangular windows on each floor and a single front door with a small stoop. Plain neutral civilian architecture, no faction accents, no signs. No team-colour panel: the house belongs to no player. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A typical city house, a two-storey brick and concrete building on a flat rectangular base plate matching a 2×2 footprint. It is about one and a half times the height of a battle tank, with a flat or low pitched roof. Rows of plain rectangular windows on each floor and a single front door with a small stoop. Plain neutral civilian architecture, no faction accents, no signs. No team-colour panel: the house belongs to no player. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, landmarks, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A typical city house, a two-storey brick and concrete building on a flat rectangular base plate matching a 2×2 footprint. It is about one and a half times the height of a battle tank, with a flat or low pitched roof. Rows of plain rectangular windows on each floor and a single front door with a small stoop. Plain neutral civilian architecture, no faction accents, no signs. No team-colour panel: the house belongs to no player. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание; дверь отдельно, если есть клип `open`
- [ ] Маска команды на зоне `#FF00FF` (не применяется: здание не принадлежит игроку)
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: размер и высота у зданий набора (README не задаёт размер, appearance.md не подтверждает размеры по видам); состав видов; ориентиры (README называет Кремль, Лувр и Аламо неподтверждёнными, appearance.md ссылается на предрелизное превью с другими ориентирами); вид при повреждении и руин. Цвет пипов: README называет цвета по типам пехоты (GI синий, призывник красный, Посвящённый фиолетовый), appearance.md и animations.md считают их неподтверждёнными (ModEnc даёт только номера кадров pips.shp).
- Спорные места, которые проверим в прототипе: читаемость пипов разных сторон при большом числе занятых зданий; как отличить здание, занятое своими, от занятого врагом (по README занятое врагом атакуется как вражеское); ориентиры делаем условными, без памятников.
