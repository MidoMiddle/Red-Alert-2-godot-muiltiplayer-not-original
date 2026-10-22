# Стена цитадели — промпты для генерации

> Фракция: Культ (аналог Юрия) · Размер: 1×1 клетка (один сегмент) · Роль: оборона (пассивная преграда)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Один сегмент стены Культа 1×1: пассивная преграда, которая не пускает пехоту и технику и прикрывает здания. В силуэте читается сплошная невысокая стена с ровным гребнем. Соединения между сегментами — открытый вопрос (см. примечания). Материал и оттенок свои вместо серого.

## Требования к этой модели

- **Размер:** 1×1 клетка = 1×1 единица Godot (один сегмент). В README: «1×1 за сегмент».
- **Высота:** выше пехотинца, ниже танка (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание.
- **Узлы в Blender:** `damage_*` для сегмента (предложение: в `animations.md` сказано, что это предложение для сегмента).
- **Цвет команды:** одна плоская панель `#FF00FF` на торце сегмента (предложение). Нужна ли окраска в цвет игрока, открытый вопрос: в оригинале стены не окрашиваются (`appearance.md`).
- **Палитра:** тёмный фиолетовый, органика, холодное свечение (предложение) — по гайду, раздел 4.
- **Особенности:**
  1. Сплошная линия с автозаполнением между узлами: читаемость препятствия важнее формы сегмента. Сохраняем.
  2. Материал и оттенок свои вместо серого. Проверить, не сливаются ли стены с фоном карты.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A single wall segment, a low solid block with a flat ridged top, taller than an infantry unit but shorter than a tank. On a flat rectangular base plate matching a 1×1 footprint, dark violet organic-industrial material with a cool glowing line along the top edge. One flat panel in pure magenta #FF00FF on the top cap of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single segment, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, один сегмент.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A single wall segment, a low solid block with a flat ridged top, taller than an infantry unit but shorter than a tank. On a flat rectangular base plate matching a 1×1 footprint, dark violet organic-industrial material with a cool glowing line along the top edge. One flat panel in pure magenta #FF00FF on the top cap of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single segment, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, background scenery, cracks, scorch marks`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A single wall segment, a low solid block with a flat ridged top, taller than an infantry unit but shorter than a tank. On a flat rectangular base plate matching a 1×1 footprint, dark violet organic-industrial material with a cool glowing line along the top edge. One flat panel in pure magenta #FF00FF on the top cap of the segment. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single segment, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 1×1 единица после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: сегмент с основанием (одна часть)
- [ ] Маска команды на зоне `#FF00FF` (если окраска в цвет игрока нужна; см. примечания)
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- **Соединения — открытый вопрос.** Промпт описывает один сегмент 1×1. Стыки, концы стены и поворот сегмента не описаны. Протяжка до четырёх клеток — механика, не модель. Решим после прототипа.
- **Окраска в цвет игрока.** В источниках стены ни у одной стороны в финальной версии не окрашиваются (C&C Wiki, `appearance.md`). Панель `#FF00FF` на торце — предложение. Если окраска не нужна, убрать панель и из промпта, и из проверки.
- Размер: README «1×1 за сегмент», без оговорок.
- Не подтверждено: вид сегмента в покое (в галерее SHP-спрайт анимирован, что именно движется, не описано), вид повреждённого сегмента, визуал установки (0:04 по README).
- Проверим в прототипе: видны ли повреждённые сегменты и не сливаются ли стены с фоном карты.
