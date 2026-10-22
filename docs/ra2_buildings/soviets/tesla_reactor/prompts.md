# Тесла-реактор — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 3×2 клеток · Роль: энергия
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Электростанция: приземистый тёмный корпус с большим светящимся шаром наверху, по которому бегут разряды. Силуэт сразу говорит, что это источник энергии: без него не открываются казармы и очистительный завод. В нашей версии шар и разряды свои, а корпус окрашен в палитру Союза.

## Требования к этой модели

- **Размер:** 3×2 клеток = 3×2 единиц Godot (рабочее значение). В таблице README 3×2; для нашей версии по плану M3 2×2.
- **Высота:** около полутора танков в высоту; шар сверху (предложение).
- **Подвижные части (отдельные объекты):** одна часть и основание.
- **Узлы в Blender:** `light_sphere` (свечение шара), `arc_*` (разряды; новый префикс, предложение), `damage_*` (дым и искры при повреждении). Предложение, см. `animations.md`.
- **Цвет команды:** плоская панель на боковой стенке корпуса; в референсе окрашена `#FF00FF`.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение). См. [гайд, раздел 4](../../generation_guide.md).
- **Особенности:** компактная вытянутая форма и шар наверху как признак электрического источника (сохраняем); форма шара и цвет разрядов свои, галерейные рисунки не копируем (делаем своим).

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A squat power generator building for a base. Low, wide dark block about one and a half tanks tall, with a large glowing sphere on top and small electric arcs crackling across it. It stands on a flat rectangular base plate matching a 3×2 footprint. Dark steel and concrete with a muted red accent. The glowing sphere is a separate detail with a soft even glow. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A squat power generator building for a base. Low, wide dark block about one and a half tanks tall, with a large glowing sphere on top and small electric arcs crackling across it. It stands on a flat rectangular base plate matching a 3×2 footprint. Dark steel and concrete with a muted red accent. The glowing sphere is a separate detail with a soft even glow. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A squat power generator building for a base. Low, wide dark block about one and a half tanks tall, with a large glowing sphere on top and small electric arcs crackling across it. It stands on a flat rectangular base plate matching a 3×2 footprint. Dark steel and concrete with a muted red accent. The glowing sphere is a separate detail with a soft even glow. One flat panel in pure magenta #FF00FF on the side wall. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 3×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- Что не подтверждено и почему (из `appearance.md` и `animations.md`):
  - высота относительно юнитов: в README её нет, в `appearance.md` «не подтверждена»; значение выше — предложение;
  - вид в покое (вариант Inactive) и при работе (вариант Active): что движется и светится, не описано; галерея C&C Wiki в этой сессии недоступна (`appearance.md`, `animations.md`);
  - вид при обычной нехватке энергии, при высасывании и при блэкауте не подтверждён: источники не описывают его для этого здания (`animations.md`);
  - повреждение (до ~50% и ~25% HP) не подтверждено; разрушение — электрический взрыв по README, не сверено в игре; обломки и выжившие не подтверждены;
  - продажа: вид процесса не описан (`appearance.md`).
- Спорные места, которые проверим в прототипе:
  - размер: README даёт 3×2 (как в оригинале), в плане M3 стоит 2×2, а README советует обдумать 3×2; от выбора зависит положение узлов (`animations.md`);
  - различимы ли Inactive и Active и видно ли, что здание даёт энергию (`appearance.md`);
  - что включает вид Active и свечение `light_sphere`: достройка или иное условие (`animations.md`);
  - читается ли отключение при высасывании и блэкауте, и нужен ли ему вид, отличный от простоя (`animations.md`).
