# Ракетный комплекс «Пэтриот» — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 1×1 клетка (рабочее значение) · Роль: оборона (ПВО)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Ракетный комплекс — основная ПВО Альянса: две пусковые установки на поворотной опоре над полусферическим основанием. В силуэте читаются опора и пусковые, что сразу показывает, что комплекс бьёт по воздуху. В нашей версии облик свой: светлая опора, синие огни; число и расположение труб — наши решения.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (рабочее значение: по README, но appearance.md пишет «не подтверждено источниками»).
- **Высота:** пусковые установки около 2 единиц над полусферой (предложение).
- **Подвижные части (отдельные объекты):** поворотная опора `spin_turret` (предложение; поворот задаётся целью, а не постоянным вращением). Трубы `tube_*` (предложение; по вики четыре трубы в установке).
- **Узлы в Blender:** `spin_turret`, `tube_*` (предложение), `light_*` — вспышка среза труб (предложение), `damage_*` — дым и огонь (предложение), `debris_*` (предложение). Клипов нет.
- **Цвет команды:** плоская панель `#FF00FF` на передней стороне полусферического основания (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Материал основания не описан (не подтверждено).
- **Особенности:** 1) сохранить две пусковые на поворотной опоре, чтобы силуэт ПВО читался сразу; 2) число и расположение труб сделать своими.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
An anti-air missile battery, two launcher pods on a rotating mount above a hemispherical base. It stands on a flat rectangular base plate matching a 1×1 footprint, the hemisphere about half a unit high and the pods about two units up. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The rotating mount and its launch tubes are separate parts. One flat panel in pure magenta #FF00FF on the front of the hemispherical base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
An anti-air missile battery, two launcher pods on a rotating mount above a hemispherical base. It stands on a flat rectangular base plate matching a 1×1 footprint, the hemisphere about half a unit high and the pods about two units up. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The rotating mount and its launch tubes are separate parts. One flat panel in pure magenta #FF00FF on the front of the hemispherical base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
An anti-air missile battery, two launcher pods on a rotating mount above a hemispherical base. It stands on a flat rectangular base plate matching a 1×1 footprint, the hemisphere about half a unit high and the pods about two units up. Light brushed metal, off-white panels, tinted glass and cool blue accent lights. The rotating mount and its launch tubes are separate parts. One flat panel in pure magenta #FF00FF on the front of the hemispherical base. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: основание, поворотная опора (`spin_turret`), трубы (`tube_*`)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: высота (appearance.md); материал основания; вспышка среза труб и поведение ракеты, которая без цели детонирует, визуально не сверены (animations.md); четыре трубы в установке — в appearance.md «вики, подтверждено», в animations.md «вики, не проверено» (сверить по вики).
- Спорные места для прототипа: читается ли поворот опоры при частой смене целей; понятно ли игроку, что ракета без цели детонирует; видно ли по форме турели, что комплекс бьёт только по воздуху.
