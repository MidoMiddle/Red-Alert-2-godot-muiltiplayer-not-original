# Дирижабль «Киров» — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Советы · Тип: авиация
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** оболочка (hull), гондола с бомбами, хвостовое оперение (всё предложено по форме); эмблема на носу своя, без акульей пасти (см. appearance.md).
- **Зоны цвета команды:** полоса вдоль верхней части оболочки (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A giant zeppelin-style airship: a long ellipsoid envelope with a rounded nose and a tapering tail with four flat cross fins. A narrow gondola hangs under the envelope, with large cylindrical bombs in open racks beneath it. A custom emblem is painted on the nose. Proportions: the envelope is about six times longer than its diameter, and the gondola is about one third of the envelope length. Neutral static pose, no pilots or crew visible in the windows. The team color zone is one flat magenta band along the top of the envelope only.
heavy Eastern-bloc industrial design, boxy shapes
Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
A giant zeppelin-style airship with a long ellipsoid envelope, four tail fins and a narrow gondola underneath with bomb racks. The team color is one flat magenta band along the top of the envelope only.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A giant zeppelin-style airship with a long ellipsoid envelope, four tail fins and a narrow gondola underneath with bomb racks. The team color is one flat magenta band along the top of the envelope only.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A giant zeppelin-style airship: a long ellipsoid envelope with a rounded nose and a tapering tail with four flat cross fins. A narrow gondola hangs under the envelope, with large cylindrical bombs in open racks beneath it. A custom emblem is painted on the nose. Proportions: the envelope is about six times longer than its diameter, and the gondola is about one third of the envelope length. Neutral static pose, no pilots or crew visible in the windows. The team color zone is one flat magenta band along the top of the envelope only.
heavy Eastern-bloc industrial design, boxy shapes
Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] оболочка, гондола и хвостовое оперение отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
