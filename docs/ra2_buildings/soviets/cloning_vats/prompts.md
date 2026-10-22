# Клонирующие чаны — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 2×2 клеток · Роль: производство (удвоение пехоты)
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Компактное здание 2×2 с прозрачными колбами: по ним читается, что здесь выращивают пехоту. В нашей версии колбы и палитра свои; советский и юрьевский варианты не копируем.

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot.
- **Высота:** корпус ниже танка, колбы около полутора высот танка (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` подвижных частей нет).
- **Узлы в Blender:** `light_*` на колбах (всплеск при клонировании; предложение), `bubbles_*` (пузырьки; предложение), `damage_*` (дым и искры, затем огонь; предложение), `debris_*` (обломки при разрушении; предложение). Клипы `open`, `close`, `active` и узлы `spin_*`, `smoke_*` не нужны.
- **Цвет команды:** панель `#FF00FF` на боковой стороне корпуса основания.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение) — по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** форму и палитру ёмкостей делаем своими; советский и юрьевский варианты не копируем.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact biotech growth building with a low rectangular housing, topped by several tall transparent cylindrical flasks filled with pale fluid, reaching about one and a half tank heights. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. Small pipes and valves connect the flasks to the housing. One flat panel in pure magenta #FF00FF on the side of the base housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact biotech growth building with a low rectangular housing, topped by several tall transparent cylindrical flasks filled with pale fluid, reaching about one and a half tank heights. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. Small pipes and valves connect the flasks to the housing. One flat panel in pure magenta #FF00FF on the side of the base housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact biotech growth building with a low rectangular housing, topped by several tall transparent cylindrical flasks filled with pale fluid, reaching about one and a half tank heights. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. Small pipes and valves connect the flasks to the housing. One flat panel in pure magenta #FF00FF on the side of the base housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Сегментацию на части не используем: части делаем в Blender.

## Проверка после генерации

- [ ] Размер 2×2 единиц после нормализации
- [ ] Pivot в центре футпринта на земле, перёд −Z
- [ ] Части разделены: корпус и основание
- [ ] Маска команды на зоне `#FF00FF`
- [ ] ≤ 20 000 треугольников LOD0, LOD1 создан
- [ ] PBR-карты на месте, текстура 2048²
- [ ] Запись в `docs/assets_registry.md`

## Примечания

- В YR (Yuri's Revenge) эти здания перешли к Юрию; у нас — Союз (аналог Советов).
- Не подтверждено и почему (из `appearance.md` и `animations.md`):
  - Высота не подтверждена (`appearance.md`). Значение «около полутора высот танка» — предложение.
  - Основа, материал колб и цвет игрока не описаны (`appearance.md`). Прозрачность колб — по `README.md`, в игре не сверено. Зону `#FF00FF` выбрали нами.
  - Вид в покое, при работе и при клонировании не подтверждён: кадры и длительность в источниках не указаны (`appearance.md`, `animations.md`). `light_*` и `bubbles_*` — наше предложение.
  - Створки и ворота: источники молчат, поэтому клипы `open` и `close` не закладываем (`animations.md`).
  - Переработка пехоты (в базовой игре): вид не подтверждён. Для нашей модели не нужна, в плане переработка — отдельное здание `recycler` (`animations.md`).
  - Разрушение и продажа не описаны (`appearance.md`, `animations.md`). Взрыв и обломки — предложение.
  - Расход энергии: страницы ModEnc расходятся, флаги здания не сверены (`animations.md`). C&C Wiki в этой сессии недоступна (402/403).
- Спорные места, которые проверим в прототипе:
  - По `README.md` здание для Союза не берём: клонирование закреплено за Культом (M11d). Набор Союза по брифу его включает. Решить до генерации.
  - Читается ли функция без подсказок и отличимо ли здание от других 2×2 (`appearance.md`).
  - Видна ли вспышка клонирования на масштабе карты и не путается ли копия с основным юнитом (`animations.md`).
  - Сохранятся ли прозрачные колбы в Meshy и Tripo или материал придётся переделать вручную.
