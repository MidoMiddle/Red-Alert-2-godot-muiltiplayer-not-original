# Хроно-харвестер (Chrono Miner) — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: техника
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** кабина, кузов для руды, колёса и телепорт-установка (отдельными узлами). Вид установки и корпус — наше предложение: в источниках не описаны.
- **Зоны цвета команды:** боковые панели кузова для руды (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A compact wheeled ore harvester, a small, sturdy and unarmed vehicle. It has a short boxy cab at the front, a squat teleport unit on the chassis behind the cab (a short cylinder with a ring frame on top), and an open ore bin at the back with a low rim. Four heavy wheels, short wheelbase, low profile and rounded corners. No weapons, no turret, no crew. The only team color zone is the side panels of the ore bin, painted flat pure magenta (#FF00FF). Proportions are compact and chunky, shorter and lower than a truck. Neutral parked pose, bin empty, no base, no ground plane.
modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
A compact unarmed wheeled ore harvester with a short cab, a squat teleport unit behind the cab, an open ore bin at the back and four wheels; the cab, bin, wheels and teleport unit are separate parts. Team color zone: only the side panels of the ore bin, flat pure magenta.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A compact unarmed wheeled ore harvester with a short cab, a squat teleport unit behind the cab, an open ore bin at the back and four wheels; the cab, bin, wheels and teleport unit are separate parts. Team color zone: only the side panels of the ore bin, flat pure magenta.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A compact wheeled ore harvester, a small, sturdy and unarmed vehicle. It has a short boxy cab at the front, a squat teleport unit on the chassis behind the cab (a short cylinder with a ring frame on top), and an open ore bin at the back with a low rim. Four heavy wheels, short wheelbase, low profile and rounded corners. No weapons, no turret, no crew. The only team color zone is the side panels of the ore bin, painted flat pure magenta (#FF00FF). Proportions are compact and chunky, shorter and lower than a truck. Neutral parked pose, bin empty, no base, no ground plane.
modern Western military design, clean angular shapes.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] кабина, кузов, колёса и телепорт-установка отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
