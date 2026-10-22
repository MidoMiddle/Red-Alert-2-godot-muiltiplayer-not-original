# Госпиталь — промпты для генерации

> Фракция: нейтральное · Размер: 6×4 клеток · Роль: поддержка (лечение пехоты)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Крупное городское здание больницы 6×4 с плоской крышей и козырьком входа. В силуэте читается больница, а функция (лечение пехоты владельца по всей карте) на здании не видна, поэтому здание почти без анимации. В нашей версии знак лечения — интерфейсный значок над пехотой, а не красный крест на фасаде: эмблемы организаций в моделях не используем (гайд, раздел 3).

## Требования к этой модели

- **Размер:** 6×4 клеток = 6×4 единиц Godot (рабочее значение: по README; по appearance.md точный размер не подтверждён).
- **Высота:** два этажа, около полутора высот танка (предложение). Высота относительно юнитов не подтверждена.
- **Подвижные части (отдельные объекты):** одна часть и основание (здание без клипов, animations.md).
- **Узлы в Blender:** `damage_*` с дымом (до ~50 % HP) и огнём (до ~25 %) — предложения. Клипы `open` и `close` не нужны: проект берёт YR-вариант, где вход в здание не требуется (README; animations.md).
- **Цвет команды:** одна плоская панель `#FF00FF` на фасаде над козырьком (предложение: цвет игрока в источниках не подтверждён).
- **Палитра:** бетон и кирпич, без фракционных акцентов и эмблем (гайд, раздел 4; предложение).
- **Особенности:**
  - Сохранить видимый знак лечения над пехотой: при глобальном лечении на поле эффекта не видно (appearance.md). Знак — UI, не часть модели.
  - Сделать своим силуэт и знак: он не обязан быть красным крестом (appearance.md).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A large city medical building, a two-storey clinic on a flat rectangular base plate matching a 6×4 footprint. The main block is about one and a half times the height of a battle tank, with a flat roof. Brick and concrete facade with rows of plain rectangular windows and a covered entrance canopy on the long side. One flat panel in pure magenta #FF00FF on the front facade above the canopy. Neutral civilian architecture, no faction accents, no emblems. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A large city medical building, a two-storey clinic on a flat rectangular base plate matching a 6×4 footprint. The main block is about one and a half times the height of a battle tank, with a flat roof. Brick and concrete facade with rows of plain rectangular windows and a covered entrance canopy on the long side. One flat panel in pure magenta #FF00FF on the front facade above the canopy. Neutral civilian architecture, no faction accents, no emblems. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logos, lettering, people, vehicles, background, ground outside the base, red cross, national flags, organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A large city medical building, a two-storey clinic on a flat rectangular base plate matching a 6×4 footprint. The main block is about one and a half times the height of a battle tank, with a flat roof. Brick and concrete facade with rows of plain rectangular windows and a covered entrance canopy on the long side. One flat panel in pure magenta #FF00FF on the front facade above the canopy. Neutral civilian architecture, no faction accents, no emblems. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 6×4 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: одна часть и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему: точный размер 6×4 (appearance.md: допустим по ModEnc, для госпиталя не сверен; мод CnC: Final War описывает свой вариант на 3×3, это мод, а не оригинал). Вид фасада и входа, цвет игрока, вид руин. Красный крест: README помещает его над вылеченной пехотой (YR), appearance.md относит к зданию, animations.md согласен с README. В промпт не включён: это эмблема организации (гайд, раздел 3), в негативном промпте он указан.
- Спорные места, которые проверим в прототипе: читаемость знака лечения над группой пехоты в больших армиях; различимость знаков госпиталя и мастерской, если оба работают рядом (комбо из README).
