# Казармы — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 2×2 клетки · Роль: производство (пехота)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Производство пехоты Культа: широкий зал с арочным порталом, из которого выходят солдаты. В силуэте читается арка входа — это одновременно признак фракции и понятный выход пехоты. Форма своя: стилизованный портал без черт лица.

## Требования к этой модели

- **Размер:** 2×2 клетки = 2×2 единицы Godot (рабочее значение). В README размер не указан («не подтверждено»); значение по аналогу — Казармы Советов, 2×2 (`soviets/barracks`). У Союзников по порту OpenRA 2×3, не подтверждено.
- **Высота:** около высоты танка, портал выше пехоты (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание (створка входа — только если решим анимировать вход, предложение).
- **Узлы в Blender:** `damage_*` (соглашение); `open` и `close` — только если вход анимируется (предложение).
- **Цвет команды:** одна плоская панель `#FF00FF` над аркой входа (предложение: цвет игрока не описан).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Вход — открытый проём: это фракционный признак и понятный выход пехоты. Сохраняем.
  2. Форму головы не копируем, делаем стилизованный портал без черт лица. Проверить, виден ли выход пехоты на масштабе карты и не перекрывает ли здание соседние клетки.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A wide training hall for infantry, with a large arched portal that units step out through and a smooth domed roof. Roughly tank height. On a flat rectangular base plate matching a 2×2 footprint, dark violet organic-industrial walls with a cool glowing accent around the arch. The portal is an open archway with no door leaf. One flat panel in pure magenta #FF00FF above the entrance arch. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A wide training hall for infantry, with a large arched portal that units step out through and a smooth domed roof. Roughly tank height. On a flat rectangular base plate matching a 2×2 footprint, dark violet organic-industrial walls with a cool glowing accent around the arch. The portal is an open archway with no door leaf. One flat panel in pure magenta #FF00FF above the entrance arch. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A wide training hall for infantry, with a large arched portal that units step out through and a smooth domed roof. Roughly tank height. On a flat rectangular base plate matching a 2×2 footprint, dark violet organic-industrial walls with a cool glowing accent around the arch. The portal is an open archway with no door leaf. One flat panel in pure magenta #FF00FF above the entrance arch. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единицы после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус с основанием (одна часть); створка входа — если решим
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Размер не подтверждён: в README без числа. Рабочее значение 2×2 по советскому аналогу; у Союзников — 2×3 (порт OpenRA, не подтверждено). Проверить в прототипе.
- Не подтверждено (appearance.md, animations.md): форма, высота, вид в покое, открытие рта при выпуске пехоты, визуал обучения, поведение без энергии, визуал при повреждении, разрушении и продаже.
- Вход анимируется или нет — не решено. Клипы `open`/`close` — предложение. Пехота — отдельные юниты, не части здания.
- Проверим в прототипе: виден ли выход пехоты из проёма на масштабе карты; не перекрывает ли здание соседние клетки при размещении.
