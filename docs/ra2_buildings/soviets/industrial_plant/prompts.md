# Промышленный завод — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 3×3 клеток · Роль: экономика (скидка на технику)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Здание 3×3 с цехами и трубами: по силуэту читается промышленный объект, а не казарма или лаборатория. В нашей версии это завод, который удешевляет технику Союза; силуэт и цвета свои, без копии галерейного вида.

## Требования к этой модели

- **Размер:** 3×3 клеток = 3×3 единиц Godot (рабочее значение: по artmd.ini, второго источника нет).
- **Высота:** около двух высот танка у корпуса цехов, трубы выше (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` подвижных частей нет).
- **Узлы в Blender:** `smoke_*` у труб, `light_*` у огней цехов, `damage_*` (лёгкий дым и искры, затем огонь и густой дым; предложение). Клипы `open`, `close`, `active` и `spin_*` не нужны.
- **Цвет команды:** панель `#FF00FF` на боковой стене главного цеха.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение) — по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** сохранить цеха и трубы как признак промышленного здания (если подтвердятся в игре); силуэт полностью свой, галерейные рисунки не используем.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A squat heavy manufacturing workshop for vehicle production, with two long halls under sloped roofs, roughly twice the height of a tank. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. A chimney stack rises from one corner, with exposed pipes along the walls. One flat panel in pure magenta #FF00FF on the side wall of the main hall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A squat heavy manufacturing workshop for vehicle production, with two long halls under sloped roofs, roughly twice the height of a tank. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. A chimney stack rises from one corner, with exposed pipes along the walls. One flat panel in pure magenta #FF00FF on the side wall of the main hall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A squat heavy manufacturing workshop for vehicle production, with two long halls under sloped roofs, roughly twice the height of a tank. It stands on a flat rectangular base plate matching a 3×3 footprint. Dark steel and concrete with a muted red accent. A chimney stack rises from one corner, with exposed pipes along the walls. One flat panel in pure magenta #FF00FF on the side wall of the main hall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
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
  - Размер 3×3 взят из artmd.ini, второго источника нет (`README.md`). В промптах помечен как рабочее значение.
  - Высота, основа и цвет игрока не описаны (`appearance.md`). C&C Wiki в этой сессии недоступна (402/403), поэтому сверить нельзя. Зону `#FF00FF` выбрали нами.
  - Цеха и трубы: по `README.md`, в игре не сверено (`appearance.md`).
  - Вид в покое: по ModEnc проигрывается «рабочая» анимация, но её вид не описан (`animations.md`). Поэтому `smoke_*` и `light_*` — предложение.
  - Вид при нехватке энергии, повреждении, разрушении и продаже не описан (`appearance.md`, `animations.md`). Страницы ModEnc расходятся о замирании анимаций при нехватке энергии.
- Спорные места, которые проверим в прототипе:
  - Отличимость от Боевой лаборатории при одинаковом размере 3×3 (`appearance.md`).
  - Постоянные дым и огни не должны читаться как «идёт производство»: здание ничего не производит (`animations.md`).
  - Гашение огней при нехватке энергии должно читаться как «бонус выключен». Это работает вместе с предложением из `README.md` отключать бонус при Low Power: в базовой игре бонус действует и без энергии.
