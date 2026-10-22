# Таня (Tanya) — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: пехота
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** два пистолета в кобурах на бёдрах и пояс с зарядами (отдельными узлами). Костюм собственный: сохранён принцип из карточки (открытый верх, камуфляж внизу, два пистолета), детали другие. Причёска и пропорции — наше предложение.
- **Зоны цвета команды:** топ (по вики, визуально не сверено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A female special forces commando, athletic and fast-looking. She wears a fitted sleeveless tactical top, camouflage cargo trousers tucked into high combat boots, and a belt with pouches. A pistol is holstered on each thigh, separate parts. A small block of demolition charges hangs from the belt at the hip. Her proportions are athletic and slim, standard for an adult woman. The only team color zone is the sleeveless top, painted flat pure magenta (#FF00FF). Hair tied back, no helmet, no face paint. Stands in a T-pose with arms straight out, no base.
modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 1500.
- Текстовое уточнение (необязательно):

```text
A female commando in a T-pose: fitted sleeveless top, camouflage cargo trousers, combat boots, and two thigh-holstered pistols as separate parts. Team color zone: only the sleeveless top, flat pure magenta.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 1500; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A female commando in a T-pose: fitted sleeveless top, camouflage cargo trousers, combat boots, and two thigh-holstered pistols as separate parts. Team color zone: only the sleeveless top, flat pure magenta.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A female special forces commando, athletic and fast-looking. She wears a fitted sleeveless tactical top, camouflage cargo trousers tucked into high combat boots, and a belt with pouches. A pistol is holstered on each thigh, separate parts. A small block of demolition charges hangs from the belt at the hip. Her proportions are athletic and slim, standard for an adult woman. The only team color zone is the sleeveless top, painted flat pure magenta (#FF00FF). Hair tied back, no helmet, no face paint. Stands in a T-pose with arms straight out, no base.
modern Western military design, clean angular shapes.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. Original design; do not imitate any existing game unit or real product.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] пистолеты и пояс с зарядами отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 1500
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
