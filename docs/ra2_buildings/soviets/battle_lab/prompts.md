# Боевая лаборатория — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 3×3 клеток · Роль: высокий тех (третий уровень)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Монументальное здание 3×3 с вертикальным акцентом в центре: по силуэту оно читается как высокий технологический уровень, а не как обычная постройка базы. Форма наша, ориентир на высотную архитектуру; галерею оригинала не копируем. Способность щита в модели не закладываем, пока не решено, войдёт ли она в проект.

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot.
- **Высота:** около трёх высот танка на пике, по центральному шпилю (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание; движущихся частей в `animations.md` нет.
- **Узлы в Blender:** шейдер `build_up` (постройка, проявление снизу вверх с подсвеченной кромкой); `damage_*` в два уровня: лёгкий дым до 50% HP, огонь и густой дым до 25% HP (предложение; соглашение M1, 4.13).
- **Цвет команды:** одна плоская полоса `#FF00FF` на крыше верхнего яруса (предложение; цвет игрока на здании не подтверждён, `appearance.md`).
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение), по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сохранить вертикальный акцент в центре, по нему здание читается как высокий технологический уровень; монументальную форму сделать в собственном стиле, галерею не копировать.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A monumental high-tech research building with a tall central spire, about three tanks high at its peak, standing on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. The stepped upper tier and the spire are fixed parts. One flat stripe in pure magenta #FF00FF across the roof of the upper tier. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A monumental high-tech research building with a tall central spire, about three tanks high at its peak, standing on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. The stepped upper tier and the spire are fixed parts. One flat stripe in pure magenta #FF00FF across the roof of the upper tier. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A monumental high-tech research building with a tall central spire, about three tanks high at its peak, standing on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. The stepped upper tier and the spire are fixed parts. One flat stripe in pure magenta #FF00FF across the roof of the upper tier. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Купола и антенны: README их упоминает, но в источниках они не подтверждены (`appearance.md`, «Силуэт и размер»). В промпте их нет.
  - Отдалённое сходство оригинала с главным зданием МГУ (C&C Wiki, `appearance.md`, «Ключевые детали») — только ориентир; в промпте не используется.
  - Анимация работы и покоя не описана (`appearance.md`, «Анимация»; `animations.md`, «Простой»). Модель статичная, `light_*` не закладываем.
  - Визуала Силового щита (YR) в источниках нет (`appearance.md`, «Ключевые детали»; `animations.md`, «Силовой щит»).
  - Длительность щита: по README 500 и 1000 кадров (соотношение 1:2), а «≈25 с» и «≈60 с» по C&C Wiki дают около 1:2,4. Пересчёт не делали (`animations.md`, «Действия»).
  - Вид продажи, разрушения и захвата не описан (`appearance.md`, «Состояния»; `animations.md`, «Повреждение и разрушение», «Продажа и захват»).
  - Высота относительно юнитов не подтверждена (`appearance.md`, «Силуэт и размер»); значение в промпте — предложение.
- Спорные места, которые проверим в прототипе:
  - Читается ли здание 3×3 как «высокий тех» без анимации покоя или нужен `light_*` (`animations.md`, «Для нашей версии», пункт 1).
  - Если щит войдёт в проект: отличим ли его эффект от Железного занавеса и заметно ли обесточивание базы (`animations.md`, «Для нашей версии», пункт 2).
  - Успеют ли стадии повреждений быть замеченными при 500 HP (`animations.md`, «Для нашей версии», пункт 3).
  - Если аналог у Альянса будет на том же ядре, видны ли различия при одинаковом размере (`appearance.md`, «Для нашей версии»).
