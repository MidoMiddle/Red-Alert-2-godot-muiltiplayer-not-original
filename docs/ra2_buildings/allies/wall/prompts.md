# Стена — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 1×1 клетка (одна секция) · Роль: оборона (пассивное препятствие)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Стена — дешёвая бетонная секция 1×1; линия стены строится протяжкой из таких секций. Модель описывает одну секцию: в силуэте читается ровный блок одинаковой высоты, чтобы линия смотрелась непрерывной. В нашей версии облик свой; стыки между секциями — открытый вопрос (см. ниже).

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (одна секция; по README).
- **Высота:** около 1.2 единицы, выше пехоты (предложение).
- **Подвижные части (отдельные объекты):** нет (одна часть и основание).
- **Соединения между секциями:** открытый вопрос. Модель сделана одной секцией без привязки к соседям; как выглядит стык (торцы, общий верхний профиль, пристыкованные блоки), не подтверждено и в M1 не решено.
- **Узлы в Blender:** `build_up` — шейдер постройки секции (предложение; не узел); `damage_*` — трещины и пыль (предложение); `debris_*` — обломки бетона (предложение). Клипов `open`, `close`, `active` нет; `spin_*` и `light_*` не назначаем.
- **Цвет команды:** открытый вопрос. В финальной версии оригинала секции серые у всех (вики, подтверждено), поэтому маску команды не строим. Зона `#FF00FF` в промпте оставлена, чтобы пайплайн был одинаковым: если цвет владельца не нужен, зону удаляем в Blender (предложение).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). На стене: светлый бетон и металл, синий акцент по кромке.
- **Особенности:** 1) сохранить одинаковую ширину и высоту секций, чтобы линия стены читалась как непрерывная; 2) цвет и текстуру сделать свои; оригинал серый и без цвета владельца, решение можно пересмотреть.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A single modular defensive wall segment, a low concrete block with a flat top and a chamfered metal cap. It stands on a flat rectangular base plate matching a 1×1 footprint, the wall about 1.2 units tall. Light concrete and brushed metal panels with a cool blue accent along the cap. The wall segment is one separate part on its base. One flat strip in pure magenta #FF00FF along the top edge of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single wall segment, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A single modular defensive wall segment, a low concrete block with a flat top and a chamfered metal cap. It stands on a flat rectangular base plate matching a 1×1 footprint, the wall about 1.2 units tall. Light concrete and brushed metal panels with a cool blue accent along the cap. The wall segment is one separate part on its base. One flat strip in pure magenta #FF00FF along the top edge of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single wall segment, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A single modular defensive wall segment, a low concrete block with a flat top and a chamfered metal cap. It stands on a flat rectangular base plate matching a 1×1 footprint, the wall about 1.2 units tall. Light concrete and brushed metal panels with a cool blue accent along the cap. The wall segment is one separate part on its base. One flat strip in pure magenta #FF00FF along the top edge of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single wall segment, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна секция (подвижных частей нет); стыки — открытый вопрос
- [ ] Маска команды на зоне `#FF00FF` (или зона удалена, если цвет владельца не нужен)
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: соединения секций (открытый вопрос); вид повреждения и разрушения не описан (appearance.md); анимации строительства в галерее вики нет, поэтому `build_up` — наше предложение (appearance.md); продажа в README не описана, Buildup у стены не подтверждён (animations.md).
- Расхождение статуса: отстройка поверх повреждённой секции в README указана как факт (вики); в animations.md — «README, вики, не проверено».
- В ограничениях слово «building» заменено на «wall segment», чтобы генератор не рисовал здание из нескольких секций.
- Спорные места для прототипа: читается ли непрерывная линия, когда секции разных владельцев стоят рядом; нужна ли анимация повреждения при 300 HP без ремонта; нужна ли вообще маска команды (animations.md предлагает `team_color` не назначать).
