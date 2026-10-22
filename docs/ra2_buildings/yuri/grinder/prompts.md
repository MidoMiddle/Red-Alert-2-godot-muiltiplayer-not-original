# Измельчитель — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 3×3 клетки · Роль: экономика (утилизация юнитов)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Измельчитель Культа: большая камера, куда отправляют юниты под контролем, и они превращаются в кредиты. В силуэте читаются широкий приёмный проём и вращающиеся валы внутри: игрок должен видеть, что юнит ушёл в переработку. Корпус и палитра свои.

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); прямого аналога нет, ближе всего очиститель руды Союзников, 3×3 (порт OpenRA, не подтверждено).
- **Высота:** около высоты танка (предложение).
- **Подвижные части (отдельные объекты):** валы — отдельные объекты, вращаются при перемалывании. В `appearance.md` и C&C Wiki их «множество»; в промпте предлагаем три (предложение). Корпус с основанием — одна часть.
- **Узлы в Blender:** `spin_*` для валов (соглашение); `active` на время перемалывания (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на боковой стене (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Видимое вращение валов во время перемалывания сохраняем: игрок понимает, что юнит ушёл в переработку.
  2. Корпус и палитра свои; валы делаем рифлёными, без шипов. Проверить, читается ли перемалывание на масштабе карты.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A heavy industrial recycling chamber that crushes captured units into credits, with a wide open intake mouth at the front. Roughly tank height. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial shell with a cool glowing accent along the intake edge. Several large rotating ribbed crusher rollers are visible inside the mouth, as separate parts. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A heavy industrial recycling chamber that crushes captured units into credits, with a wide open intake mouth at the front. Roughly tank height. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial shell with a cool glowing accent along the intake edge. Several large rotating ribbed crusher rollers are visible inside the mouth, as separate parts. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A heavy industrial recycling chamber that crushes captured units into credits, with a wide open intake mouth at the front. Roughly tank height. On a flat rectangular base plate matching a 3×3 footprint, a dark violet organic-industrial shell with a cool glowing accent along the intake edge. Several large rotating ribbed crusher rollers are visible inside the mouth, as separate parts. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием (одна часть); валы (три отдельных объекта, предложение)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. 3×3 — рабочее значение; ближайший аналог — очиститель руды Союзников (порт OpenRA, не подтверждено). Прямого аналога нет.
- Не подтверждено (README, appearance.md, animations.md): энергия (в карточке не указана), форма корпуса, высота, число валов, вид простоя (по ModEnc есть вторая idle-анимация, которая играет без перемалывания; её вид не описан), визуал при повреждении, разрушении и продаже.
- В README описан баг: здание иногда залипает на кадре с видимыми валами. Проверить, что в нашей версии он не повторяется.
- Проверим в прототипе: читается ли перемалывание на масштабе карты и видна ли разница между простоем и перемалыванием.
