# «Морской скорпион» — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Советы · Тип: флот
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** корпус (hull), башня с флак-орудием (turret), ствол (barrel); орудие посередине судна, как на вокселе; корпус и пропорции предложены.
- **Зоны цвета команды:** крыша ходовой рубки (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A small, fast patrol boat with a narrow, sharp-bowed hull about four times longer than it is wide. A low bridge cabin sits slightly forward of the middle, and one flak gun with a short barrel stands on a round turret mount amidships. Proportions: the gun turret is about one fifth of the hull length, and the stern is squared off. Neutral static pose, gun pointing forward, isolated, with no water, no waves and no base. The team color zone is the flat roof panel of the bridge cabin only.
heavy Eastern-bloc industrial design, boxy shapes
Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
A small, fast patrol boat with a sharp bow, a low bridge cabin and one flak gun on a round mount amidships. The team color is the flat magenta roof panel of the bridge cabin only.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A small, fast patrol boat with a sharp bow, a low bridge cabin and one flak gun on a round mount amidships. The team color is the flat magenta roof panel of the bridge cabin only.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A small, fast patrol boat with a narrow, sharp-bowed hull about four times longer than it is wide. A low bridge cabin sits slightly forward of the middle, and one flak gun with a short barrel stands on a round turret mount amidships. Proportions: the gun turret is about one fifth of the hull length, and the stern is squared off. Neutral static pose, gun pointing forward, isolated, with no water, no waves and no base. The team color zone is the flat roof panel of the bridge cabin only.
heavy Eastern-bloc industrial design, boxy shapes
Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] корпус, башня и ствол отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
