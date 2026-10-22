# Мобильный сборочный цех, MCV (Allied MCV) — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: техника
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** кабина, колёса и свёрнутый строительный модуль (отдельными узлами). Корпус шасси — hull. Колёса (шесть), пропорции и вид модуля — наше предложение: appearance.md их не описывает.
- **Зоны цвета команды:** двери кабины и боковые панели модуля (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A large, unarmed wheeled construction vehicle, a heavy six-wheel chassis with a boxy truck cab at the front and a folded construction module on the flat cargo bed behind it. The cab has angular sides and a wide windshield, the module is a closed box with rounded corners and folded panels. No weapons, no turret, no antennas, no crew in the cab. The only team color zone is the cab doors and the side panels of the module housing, painted flat pure magenta (#FF00FF). Long, low and heavy proportions with big wheels. Neutral parked pose, wheels in place, no ground plane, no base.
modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
A large unarmed wheeled construction vehicle: a six-wheel chassis with a boxy cab and a folded construction module on the cargo bed, with the cab and module as separate parts. Team color zones: only the cab doors and the side panels of the module housing, flat pure magenta.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A large unarmed wheeled construction vehicle: a six-wheel chassis with a boxy cab and a folded construction module on the cargo bed, with the cab and module as separate parts. Team color zones: only the cab doors and the side panels of the module housing, flat pure magenta.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A large, unarmed wheeled construction vehicle, a heavy six-wheel chassis with a boxy truck cab at the front and a folded construction module on the flat cargo bed behind it. The cab has angular sides and a wide windshield, the module is a closed box with rounded corners and folded panels. No weapons, no turret, no antennas, no crew in the cab. The only team color zone is the cab doors and the side panels of the module housing, painted flat pure magenta (#FF00FF). Long, low and heavy proportions with big wheels. Neutral parked pose, wheels in place, no ground plane, no base.
modern Western military design, clean angular shapes.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. Original design; do not imitate any existing game unit or real product.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] кабина, колёса и модуль отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
