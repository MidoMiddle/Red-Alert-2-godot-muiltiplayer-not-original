# Безумный Иван — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Советы · Тип: пехота
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** нет, одна модель. Одежда, фитиль и вид динамита в источниках не описаны, предложены.
- **Зоны цвета команды:** левая нарукавная повязка (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A wiry demolition specialist in a light work jacket and trousers, with a wide grin and a wild look in the eyes. Bundles of dynamite sticks are strapped across the chest and to the belt, with a short fuse cord hanging from one bundle. He carries no rifle and no heavy gear, only his explosives. Average height, light and quick. The explosives are the main visual idea, readable at a glance. A flat pure magenta (#FF00FF) armband on the left upper arm marks the team color.

Design language: heavy Eastern-bloc industrial design, boxy shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 1500.
- Текстовое уточнение (необязательно):

```text
One wiry infantry figure in a light work jacket and trousers, with bundles of dynamite sticks strapped across the chest and to the belt. The only team color zone is a flat pure magenta armband on the left upper arm.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 1500; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
One wiry infantry figure in a light work jacket and trousers, with bundles of dynamite sticks strapped across the chest and to the belt. The only team color zone is a flat pure magenta armband on the left upper arm.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A wiry demolition specialist in a light work jacket and trousers, with a wide grin and a wild look in the eyes. Bundles of dynamite sticks are strapped across the chest and to the belt, with a short fuse cord hanging from one bundle. He carries no rifle and no heavy gear, only his explosives. Average height, light and quick. The explosives are the main visual idea, readable at a glance. A flat pure magenta (#FF00FF) armband on the left upper arm marks the team color.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] отдельных узлов нет: фигура цела, динамит на ней
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 1500
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
