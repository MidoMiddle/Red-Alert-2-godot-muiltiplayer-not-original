# Гатлинг-пушка — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 1×1 клетка · Роль: оборона (против пехоты и авиации)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Базовая оборонительная башня Культа против пехоты и авиации: пара многоствольных стволов на станине. В силуэте читаются стволы, которые раскручиваются при долгой стрельбе, и это показывает набранную мощь. Число и расположение стволов, станина свои.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение). В README: «1×1 (не подтверждено)»; аналоги Союзников и Советов тоже 1×1.
- **Высота:** ниже танка, выше пехоты (предложение).
- **Подвижные части (отдельные объекты):** ствольный блок — отдельный объект, вращается при стрельбе (`spin_*`); станина с основанием — одна часть.
- **Узлы в Blender:** `spin_*` для ствольного блока (соглашение); `damage_*` (соглашение). Вспышку `muzzle_*` из `animations.md` в этот список не включаем: её нет в гайде, см. примечания.
- **Цвет команды:** одна плоская панель `#FF00FF` на станине (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Раскрутку стволов при долгой стрельбе сохраняем как визуальный признак набранной мощи.
  2. Число и расположение стволов, форму станины делаем свои; пару стволов не копируем один в один.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact twin rotary multi-barrel gun turret on a squat armoured mount, able to fire at ground and air targets. Shorter than a tank. On a flat rectangular base plate matching a 1×1 footprint, a dark violet organic-industrial housing with a cool glowing accent ring around the mount. The rotary barrel block is a separate part that spins during fire. One flat panel in pure magenta #FF00FF on the mount. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact twin rotary multi-barrel gun turret on a squat armoured mount, able to fire at ground and air targets. Shorter than a tank. On a flat rectangular base plate matching a 1×1 footprint, a dark violet organic-industrial housing with a cool glowing accent ring around the mount. The rotary barrel block is a separate part that spins during fire. One flat panel in pure magenta #FF00FF on the mount. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact twin rotary multi-barrel gun turret on a squat armoured mount, able to fire at ground and air targets. Shorter than a tank. On a flat rectangular base plate matching a 1×1 footprint, a dark violet organic-industrial housing with a cool glowing accent ring around the mount. The rotary barrel block is a separate part that spins during fire. One flat panel in pure magenta #FF00FF on the mount. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: станина с основанием (одна часть); ствольный блок (отдельный объект)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён в README («1×1 (не подтверждено)»). Оставляем 1×1: аналоги Союзников (зенитный комплекс) и Советов (зенитная пушка) тоже 1×1.
- Название оригинала в README и C&C Wiki записано с опечаткой «Gattling» (C&C Wiki отмечает это как sic). В файлах и промптах используем описательное название.
- Узел `muzzle_*` (вспышка выстрела) из `animations.md` не входит в список узлов гайда (`spin_*`, `smoke_*`, `light_*`, `damage_*`). Решить: добавить его в гайд или не использовать.
- **Расхождение.** README: поведение при дефиците энергии «не подтверждено». `animations.md` ссылается на cncnz: башня деактивируется при низкой энергии базы. Нужна проверка.
- Не подтверждено: вид в покое, вспышки и отдача, визуал при повреждении, разрушении и продаже. Три ступени раскрутки взяты из README и cncnz; их визуал — предложение.
- Проверим в прототипе: видна ли текущая ступень раскрутки на масштабе карты.
