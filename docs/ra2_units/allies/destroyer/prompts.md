# Эсминец — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: флот
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** hull (корпус), barrel (155-мм неподвижное орудие на носу). Вертолётная площадка на корме — часть палубы; сам вертолёт — отдельный юнит, в модель не входит.
- **Зоны цвета команды:** боковые панели надстройки (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
Medium warship with a long, low hull and a single fixed gun on the bow, not on a rotating mount. The hull has a sharp bow, a flat deck and a plain outline. A simple superstructure with flat panels sits amidships, and a flat helicopter pad on the stern stays empty. The deck is clean, with no extra equipment. Proportions: medium-sized, sturdy and long, with a low freeboard. Team color zones, flat magenta: the side panels of the superstructure. No water, no waves, no base; neutral static pose.

modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
Medium warship with a long, low hull, a single fixed gun on the bow and a simple superstructure, plus an empty helicopter pad on the stern. Separate hull and gun, with team color (flat magenta) on the superstructure side panels; no water or waves.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
Medium warship with a long, low hull, a single fixed gun on the bow and a simple superstructure, plus an empty helicopter pad on the stern. Separate hull and gun, with team color (flat magenta) on the superstructure side panels; no water or waves.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
Medium warship with a long, low hull and a single fixed gun on the bow, not on a rotating mount. The hull has a sharp bow, a flat deck and a plain outline. A simple superstructure with flat panels sits amidships, and a flat helicopter pad on the stern stays empty. The deck is clean, with no extra equipment. Proportions: medium-sized, sturdy and long, with a low freeboard. Team color zones, flat magenta: the side panels of the superstructure. No water, no waves, no base; neutral static pose.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. Original design; do not imitate any existing game unit or real product.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] корпус и орудие отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
