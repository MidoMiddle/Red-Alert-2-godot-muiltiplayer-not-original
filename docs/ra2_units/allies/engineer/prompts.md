# Инженер (Engineer) — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: пехота
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** ящик инструментов на поясе (отдельным узлом). Одежда, каска и ящик — наше предложение: appearance.md их не описывает.
- **Зоны цвета команды:** каска (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
An unarmed infantry technician, a specialist who is clearly not a fighter. He wears a plain hard hat, a work jacket with rolled sleeves over loose trousers, and heavy work boots. A metal toolbox hangs on the belt at the hip, a separate part, with a small pouch on the other side. He carries no weapon of any kind and has no vest or rifle, so he reads as a non-combatant at a glance. Standard human proportions, slightly lighter build than a soldier. The team color zone is the hard hat, painted flat pure magenta (#FF00FF). Stands in a T-pose with arms straight out and empty hands; no backpack, no base.
modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 1500.
- Текстовое уточнение (необязательно):

```text
An unarmed infantry technician in a T-pose: work jacket, heavy boots, and a metal toolbox on the belt as a separate part. Team color zone: only the hard hat, flat pure magenta.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 1500; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
An unarmed infantry technician in a T-pose: work jacket, heavy boots, and a metal toolbox on the belt as a separate part. Team color zone: only the hard hat, flat pure magenta.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
An unarmed infantry technician, a specialist who is clearly not a fighter. He wears a plain hard hat, a work jacket with rolled sleeves over loose trousers, and heavy work boots. A metal toolbox hangs on the belt at the hip, a separate part, with a small pouch on the other side. He carries no weapon of any kind and has no vest or rifle, so he reads as a non-combatant at a glance. Standard human proportions, slightly lighter build than a soldier. The team color zone is the hard hat, painted flat pure magenta (#FF00FF). Stands in a T-pose with arms straight out and empty hands; no backpack, no base.
modern Western military design, clean angular shapes.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] ящик инструментов отделяется
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 1500
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
