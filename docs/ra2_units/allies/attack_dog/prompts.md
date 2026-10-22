# Боевая собака (Attack Dog) — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Союзники · Тип: пехота
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** нет, это одна фигура. Порода и окрас в промте — собственные: appearance.md их не подтверждает.
- **Зоны цвета команды:** ошейник (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A muscular combat dog, a trained guard animal with a broad chest, deep body, long strong legs and a wedge-shaped head with a powerful jaw and upright ears. Its short coat is dark gray with a lighter muzzle and paws; no breed, no pattern, no markings. The tail is long and held low. A thick leather collar is the only team color zone, painted flat pure magenta (#FF00FF). The dog stands on all four legs in a neutral stance, not a T-pose, head level, looking forward. No harness, no leash, no base, no ground shadow. Compact, athletic proportions, a readable animal silhouette.
modern Western military design, clean angular shapes.

Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 1500.
- Текстовое уточнение (необязательно):

```text
A four-legged combat dog standing in a neutral stance, not a T-pose: one body with four legs, head and tail, no separate parts. Team color zone: only the leather collar, flat pure magenta.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 1500; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A four-legged combat dog standing in a neutral stance, not a T-pose: one body with four legs, head and tail, no separate parts. Team color zone: only the leather collar, flat pure magenta.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A muscular combat dog, a trained guard animal with a broad chest, deep body, long strong legs and a wedge-shaped head with a powerful jaw and upright ears. Its short coat is dark gray with a lighter muzzle and paws; no breed, no pattern, no markings. The tail is long and held low. A thick leather collar is the only team color zone, painted flat pure magenta (#FF00FF). The dog stands on all four legs in a neutral stance, not a T-pose, head level, looking forward. No harness, no leash, no base, no ground shadow. Compact, athletic proportions, a readable animal silhouette.
modern Western military design, clean angular shapes.

Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. Original design; do not imitate any existing game unit or real product.

Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] отдельных частей нет, собака — одна фигура
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 1500
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
