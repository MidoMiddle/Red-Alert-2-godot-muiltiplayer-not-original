# Боевая собака — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Советы · Тип: пехота
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** нет, одна модель. Порода не копируется 1:1, силуэт собственный (см. appearance.md). Поза: стойка на четырёх лапах, не T-поза.
- **Зоны цвета команды:** ошейник (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A lean, fast, wolf-like war dog, not a specific breed, with a thick double coat, erect triangular ears, a muscular chest and a bushy tail carried low. Built as a scout and anti-infantry attacker: long legs, narrow waist, alert head held level. It stands on four legs in a neutral stance, not a T-pose. A wide collar around the neck is flat pure magenta (#FF00FF) and marks the team color. No armor, no harness, no saddle; the body is light and fast.

Design language: heavy Eastern-bloc industrial design, boxy shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 1500.
- Текстовое уточнение (необязательно):

```text
One lean, fast, wolf-like war dog standing on four legs in a neutral stance, not a T-pose, with a thick double coat and erect ears. The only team color zone is a flat pure magenta collar around the neck.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 1500; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
One lean, fast, wolf-like war dog standing on four legs in a neutral stance, not a T-pose, with a thick double coat and erect ears. The only team color zone is a flat pure magenta collar around the neck.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A lean, fast, wolf-like war dog, not a specific breed, with a thick double coat, erect triangular ears, a muscular chest and a bushy tail carried low. Built as a scout and anti-infantry attacker: long legs, narrow waist, alert head held level. It stands on four legs in a neutral stance, not a T-pose. A wide collar around the neck is flat pure magenta (#FF00FF) and marks the team color. No armor, no harness, no saddle; the body is light and fast.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. Original design; do not imitate any existing game unit or real product.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] отдельных узлов нет: четыре лапы, хвост и голова на месте, одна модель
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 1500
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
