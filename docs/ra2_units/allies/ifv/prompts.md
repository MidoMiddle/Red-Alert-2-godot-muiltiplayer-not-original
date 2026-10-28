# БМП — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: техника
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** hull (корпус на колёсах), turret (оружейный модуль на крыше; в модели вариант с ракетами по умолчанию).
- **Зоны цвета команды:** боковые панели корпуса и задняя стенка оружейного модуля (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
Light wheeled armored vehicle with a low, boxy hull and large tires. A single weapon pod sits on the roof of the hull. In its default form the pod holds a twin rocket launcher, its two tubes angled upward, and it is the most recognizable feature, so it must read as a separate module. The rest of the vehicle is compact and simple. Proportions: short, low and fast-looking, not bulky. Team color zones, flat magenta: the side panels of the hull and the rear face of the weapon pod. Neutral static pose, no passenger visible.

modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
Light wheeled armored vehicle with large tires, a low boxy hull and a separate weapon pod on the roof that holds a twin rocket launcher. Separate hull and weapon pod, with team color (flat magenta) on the hull side panels and the rear face of the pod.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
Light wheeled armored vehicle with large tires, a low boxy hull and a separate weapon pod on the roof that holds a twin rocket launcher. Separate hull and weapon pod, with team color (flat magenta) on the hull side panels and the rear face of the pod.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
Light wheeled armored vehicle with a low, boxy hull and large tires. A single weapon pod sits on the roof of the hull. In its default form the pod holds a twin rocket launcher, its two tubes angled upward, and it is the most recognizable feature, so it must read as a separate module. The rest of the vehicle is compact and simple. Proportions: short, low and fast-looking, not bulky. Team color zones, flat magenta: the side panels of the hull and the rear face of the weapon pod. Neutral static pose, no passenger visible.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] корпус и оружейный модуль отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
