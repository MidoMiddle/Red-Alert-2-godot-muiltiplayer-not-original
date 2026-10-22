# Снайпер (Sniper) — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: пехота
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** винтовка за спиной (отдельным узлом). Форма, камуфляж и головной убор в источниках не описаны: в промте — наше предложение.
- **Зоны цвета команды:** лента на полевой кепке (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A long-range marksman, a patient soldier who waits in cover. He wears a light olive field uniform with a plain tactical vest, a soft field cap, and sturdy boots. A long, heavy bolt-action rifle with a scope and a muzzle brake is slung across his back, its barrel rising above one shoulder, a separate part. Standard human proportions, slim and steady build. The team color zone is the band around the field cap, painted flat pure magenta (#FF00FF). Nothing else is team colored. Stands in a T-pose with arms straight out, no bipod, no camouflage net, no base.
modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 1500.
- Текстовое уточнение (необязательно):

```text
A long-range marksman in a T-pose in a light olive uniform and field cap: a long bolt-action rifle with a scope, slung across the back as a separate part. Team color zone: only the band of the cap, flat pure magenta.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 1500; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A long-range marksman in a T-pose in a light olive uniform and field cap: a long bolt-action rifle with a scope, slung across the back as a separate part. Team color zone: only the band of the cap, flat pure magenta.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A long-range marksman, a patient soldier who waits in cover. He wears a light olive field uniform with a plain tactical vest, a soft field cap, and sturdy boots. A long, heavy bolt-action rifle with a scope and a muzzle brake is slung across his back, its barrel rising above one shoulder, a separate part. Standard human proportions, slim and steady build. The team color zone is the band around the field cap, painted flat pure magenta (#FF00FF). Nothing else is team colored. Stands in a T-pose with arms straight out, no bipod, no camouflage net, no base.
modern Western military design, clean angular shapes.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. Original design; do not imitate any existing game unit or real product.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] винтовка отделяется
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 1500
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
