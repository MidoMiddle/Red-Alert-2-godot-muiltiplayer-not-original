# Пси-сенсор — промпты для генерации

> Фракция: Союз (аналог Советов) · Размер: 2×2 клеток · Роль: информация
> Связанные: [README](README.md) · [Внешний вид](appearance.md) · [Анимации](animations.md) · [Гайд по генерации](../../generation_guide.md)

## Что получаем

Небольшое здание 2×2 с антенной-излучателем: по силуэту читается разведка, а не оружие. В нашей версии антенна и облик излучателя свои; радиус 15 клеток показывает интерфейс, а не модель.

## Требования к этой модели

- **Размер:** 2×2 клеток = 2×2 единиц Godot.
- **Высота:** корпус около одной высоты танка, антенна до двух (предложение; в README высоты нет).
- **Подвижные части (отдельные объекты):** одна часть и основание (в `animations.md` вращение антенны не подтверждено).
- **Узлы в Blender:** `light_*` (индикатор работы; гаснет без энергии; предложение), `damage_*` (огонь и дым; предложение). `spin_*` для антенны — только если вращение подтвердится (см. «Примечания»). Клипы `open`, `close`, `active` и `smoke_*` не нужны.
- **Цвет команды:** панель `#FF00FF` на крыше корпуса.
- **Палитра:** тёмная сталь, бетон, приглушённый красный акцент (предложение) — по [гайду, раздел 4](../../generation_guide.md).
- **Особенности:** антенна и облик излучателя свои; круговой индикатор радиуса остаётся в интерфейсе, а не в модели.

## Промпт 1. Референс (ChatGPT, генерация изображения)

Вставьте блок в ChatGPT как есть (раздел генерации изображений).

```
A compact detection station that watches for approaching enemies, with a squat rounded housing about one tank tall. A single tall emitter antenna rises from the roof to up to two tank heights, with a small glowing tip. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. Thin cable conduits run along the base. One flat panel in pure magenta #FF00FF on the roof of the housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Параметры: квадрат 1:1, вид 3/4 (поворот 45°, камера чуть сверху, около 30°), фон однотонный серый, одно здание.

## Промпт 2. Meshy (image-to-3D по референсу из промпта 1)

```
A compact detection station that watches for approaching enemies, with a squat rounded housing about one tank tall. A single tall emitter antenna rises from the roof to up to two tank heights, with a small glowing tip. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. Thin cable conduits run along the base. One flat panel in pure magenta #FF00FF on the roof of the housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
```

Настройки — в [гайде, раздел 6](../../generation_guide.md). Негативный промпт — только если поле есть: `text, logo, lettering, watermark, people, vehicles, extra ground outside the base`.

## Промпт 3. Tripo (image-to-3D по референсу из промпта 1)

```
A compact detection station that watches for approaching enemies, with a squat rounded housing about one tank tall. A single tall emitter antenna rises from the roof to up to two tank heights, with a small glowing tip. It stands on a flat rectangular base plate matching a 2×2 footprint. Dark steel and concrete with a muted red accent. Thin cable conduits run along the base. One flat panel in pure magenta #FF00FF on the roof of the housing. Stylised-realistic modern RTS game asset, clean hard-surface geometry, readable silhouette at isometric distance, PBR materials. Single building, no background, no text, no logos, no people, no vehicles.
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
  - Высота, основа и цвет игрока не описаны (`appearance.md`). Высота в промпте — предложение. Зону `#FF00FF` выбрали нами.
  - Антенна-излучатель: по `README.md`, в игре не сверено. Её вращение не подтверждено, `spin_*` — предложение (`animations.md`).
  - Вид в покое и при работе не описан. По `README.md` без энергии сенсор не работает, но вид не описан. Страницы ModEnc расходятся о замирании анимаций (`appearance.md`, `animations.md`).
  - Пунктирные линии намерений и `PSIWARN` рисует клиент, а не модель. Их вид в источниках не описан (`animations.md`).
  - Раскрытие шпионов визуально не описано (`animations.md`).
  - Buildup и активная анимация: кадры и стадии не подтверждены. C&C Wiki в этой сессии недоступна (402/403) (`appearance.md`, `animations.md`).
  - Разрушение и продажа не описаны (`appearance.md`, `animations.md`). Взрыв и обломки — предложение.
- Спорные места, которые проверим в прототипе:
  - По `README.md` для Союза не берём: механика Культа (психический радар, M11d). Набор Союза по брифу его включает. Решить до генерации.
  - Читается ли состояние «работает / нет энергии» без вращения антенны (`animations.md`).
  - Не путается ли силуэт с радарной вышкой 2×2 (`appearance.md`).
