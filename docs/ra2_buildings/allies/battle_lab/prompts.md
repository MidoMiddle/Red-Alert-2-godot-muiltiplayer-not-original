# Боевая лаборатория — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 3×3 клетки · Роль: высокий тех (тех-3); в YR — защитный щит
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Боевая лаборатория — высокое техническое здание, которое открывает тех-3 Альянса; оно заметное и хрупкое. В силуэте читается высокий корпус, выше соседних построек. В нашей версии облик свой: светлые панели с синей подсветкой; в YR здание даёт защитный щит, подсветку корпуса используем как его признак (наше решение).

## Требования к этой модели

- **Размер:** 3×3 клетки = 3×3 единицы Godot (рабочее значение: в README «не подтверждено», порт OpenRA даёт 3×2).
- **Высота:** около 3.5 единицы, заметно выше соседних построек (предложение; высокий корпус по вики подтверждён, точная высота — нет).
- **Подвижные части (отдельные объекты):** нет (одна часть и основание).
- **Узлы в Blender:** `light_shield` — подсветка корпуса, пока щит активен (предложение); `light_*` — гаснут без питания (предложение); `damage_*` — дым и огонь (предложение); `debris_*` (предложение). Клипов нет.
- **Цвет команды:** плоская панель `#FF00FF` на фасаде, в средней части корпуса (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Материал корпуса в источниках не описан.
- **Особенности:** 1) сохранить высокий, заметный силуэт, чтобы здание читалось издалека как тех-центр; 2) форму и материалы сделать свои: светлые панели с синими деталями — стиль фракции (наше решение).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A tall high-technology research laboratory tower with a slim upper section and a flat roof. It stands on a flat rectangular base plate matching a 3×3 footprint, the main body about three and a half units tall. Light brushed metal panels, off-white trim, tinted glass bands and cool blue accent lights. A glowing accent ring around the upper section is a separate part. One flat panel in pure magenta #FF00FF on the middle of the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A tall high-technology research laboratory tower with a slim upper section and a flat roof. It stands on a flat rectangular base plate matching a 3×3 footprint, the main body about three and a half units tall. Light brushed metal panels, off-white trim, tinted glass bands and cool blue accent lights. A glowing accent ring around the upper section is a separate part. One flat panel in pure magenta #FF00FF on the middle of the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A tall high-technology research laboratory tower with a slim upper section and a flat roof. It stands on a flat rectangular base plate matching a 3×3 footprint, the main body about three and a half units tall. Light brushed metal panels, off-white trim, tinted glass bands and cool blue accent lights. A glowing accent ring around the upper section is a separate part. One flat panel in pure magenta #FF00FF on the middle of the front face. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×3 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание (подвижных частей нет)
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер 3×3 (README; порт OpenRA даёт 3×2); точная высота; вид щита (appearance.md: «не подтверждено»); материал корпуса и цвет игрока.
- Спорные места для прототипа: не мешает ли высота собственной авиации (README: высота как артефакт RA2 повторять не нужно; appearance.md); читается ли щит на расстоянии по подсветке одного здания (animations.md).
