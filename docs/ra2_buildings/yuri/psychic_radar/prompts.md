# Пси-радар — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 2×2 клетки · Роль: информация (радар и детектор)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Радар Культа, который показывает, куда направлены атаки противника, и видит шпионов и невидимые цели. В силуэте читаются мачта и тарелка сенсора, по которым понятно, что это детектор, а не оборона. Антенна и корпус свои, в палитре фракции.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — Пси-сенсор Советов, 2×2.
- **Высота:** заметно выше танка (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (вращение антенны не подтверждено; `spin_*` — только если решим, предложение).
- **Узлы в Blender:** клип `active` для работы радара (соглашение); `light_*` — индикатор работы и заряда способности (предложение); `damage_*` (соглашение).
- **Цвет команды:** одна плоская панель `#FF00FF` на корпусе (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Признак радара по силуэту (мачта и тарелка) сохраняем: иначе функция не читается.
  2. Форму антенны делаем свою. Проверить, не путается ли здание с радаром другой стороны.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A detection tower: a slender mast topped by a fixed dish-shaped sensor head that scans for hidden energy signatures. Taller than a tank. On a flat rectangular base plate matching a 2×2 footprint, a low dark violet organic housing with a cool glowing accent ring around the mast base. The mast and sensor head form one solid part with no moving parts. One flat panel in pure magenta #FF00FF on the housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A detection tower: a slender mast topped by a fixed dish-shaped sensor head that scans for hidden energy signatures. Taller than a tank. On a flat rectangular base plate matching a 2×2 footprint, a low dark violet organic housing with a cool glowing accent ring around the mast base. The mast and sensor head form one solid part with no moving parts. One flat panel in pure magenta #FF00FF on the housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A detection tower: a slender mast topped by a fixed dish-shaped sensor head that scans for hidden energy signatures. Taller than a tank. On a flat rectangular base plate matching a 2×2 footprint, a low dark violet organic housing with a cool glowing accent ring around the mast base. The mast and sensor head form one solid part with no moving parts. One flat panel in pure magenta #FF00FF on the housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием и мачтой (одна часть)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. Рабочее значение 2×2 по советскому аналогу (Пси-сенсор, 2×2).
- Расхождение: `appearance.md` пишет, что при нехватке энергии активная анимация замирает (одна из страниц ModEnc); `animations.md` пишет, что по умолчанию она играет, и сам отмечает расхождение с `appearance.md`. Флаг здания не сверён.
- Не подтверждено: антенна или тарелка (форма и вращение), вид в покое, визуал способности «Пси-раскрытие» на самом здании, цвет и свет, визуал при повреждении, разрушении и продаже.
- Проверим в прототипе: читается ли радиус обнаружения (15 клеток по README) и отличается ли активный радар от неактивного.
