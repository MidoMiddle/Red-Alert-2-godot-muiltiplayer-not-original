# Центр управления роботами — промпты для генерации

> Фракция: Альянс (аналог Союзников) · Размер: 2×2 клетки (рабочее значение) · Роль: поддержка (управление роботами)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Центр управления роботами — небольшое здание, без которого роботы встают: оператор сидит в центре, а не в машине. В силуэте читаются невысокий корпус и антенная мачта. В нашей версии облик свой; связь «центр — роботы» показываем индикатором питания или линией.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение: в README размер не указан, стоит «не подтверждено»; 2×2 предложено для промптов, сверить в M1).
- **Высота:** корпус около 1.5 единицы, мачта до 2.5 (предложение; силуэт и высота в источниках не описаны).
- **Подвижные части (отдельные объекты):** нет (одна часть и основание). Клип `active` не назначаем, пока движение не подтверждено.
- **Узлы в Blender:** `light_*` — индикаторы питания (предложение); `damage_*` — дым и искры (предложение); `debris_*` (предложение). Клип `active` — только если движение подтвердится.
- **Цвет команды:** плоская панель `#FF00FF` на фасаде у входа (предложение: цвет игрока не подтверждён).
- **Палитра:** по гайду, раздел 4, для Альянса: светлый металл, стекло, холодный синий акцент (предложение; палитра не утверждена). Основа и акценты — не подтверждены.
- **Особенности:** 1) сохранить видимую связь «центр — роботы», чтобы игрок понимал, почему роботы встают; 2) облик центра сделать своим; связь показать индикатором или линией.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact remote-command building that controls unmanned hover machines, with glass booths on the front and a mast on the roof. It stands on a flat rectangular base plate matching a 2×2 footprint, the housing about 1.5 units tall, the mast up to 2.5. Light brushed metal, off-white panels, tinted glass and cool blue indicator lights. The antenna mast and the indicator strip are separate parts. One flat panel in pure magenta #FF00FF on the front face beside the entrance. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact remote-command building that controls unmanned hover machines, with glass booths on the front and a mast on the roof. It stands on a flat rectangular base plate matching a 2×2 footprint, the housing about 1.5 units tall, the mast up to 2.5. Light brushed metal, off-white panels, tinted glass and cool blue indicator lights. The antenna mast and the indicator strip are separate parts. One flat panel in pure magenta #FF00FF on the front face beside the entrance. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, background, ground outside the footprint, people, vehicles, real national or organisation emblems`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact remote-command building that controls unmanned hover machines, with glass booths on the front and a mast on the roof. It stands on a flat rectangular base plate matching a 2×2 footprint, the housing about 1.5 units tall, the mast up to 2.5. Light brushed metal, off-white panels, tinted glass and cool blue indicator lights. The antenna mast and the indicator strip are separate parts. One flat panel in pure magenta #FF00FF on the front face beside the entrance. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание, антенная мачта
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Не подтверждено: размер — в README значения нет («не подтверждено», порт OpenRA не содержит зданий YR), 2×2 взято как рабочее предложение; силуэт и высота не описаны (appearance.md); «Active animation» в галерее вики подписана, но её содержание не описано; какой ключ ModEnc у этого здания — неизвестно (animations.md).
- Спорные места для прототипа: читается ли по картинке, что центр запитан и управляет роботами; различается ли отключение от нехватки энергии и кнопкой отключения питания (animations.md).
