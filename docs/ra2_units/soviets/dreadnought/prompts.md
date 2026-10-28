# Дредноут — промты для генерации

> Справочник по оригинальной Red Alert 2 (без дополнения Yuri's Revenge) · Создан: 2026-10-22
> Фракция: Советы · Тип: флот
> Карточка: [README.md](README.md) · Внешний вид: [appearance.md](appearance.md) · Общие требования: [generation_requirements.md](../../generation_requirements.md)

Порядок: картинка-референс (шаг 1) → 3D в Meshy или Tripo (шаг 2) → чистка и проверка по [общим требованиям](../../generation_requirements.md). Промты на английском, пояснения на русском. Описание формы взято из [appearance.md](appearance.md). Цвета в промтах — наши предложения, в источниках они не подтверждены.

---

## Части и цвет команды

- **Отдельные узлы:** корпус (hull), надстройка с мостиком (superstructure), две двойные пусковые установки ракет (по две трубы на установку); корпус и пропорции предложены.
- **Зоны цвета команды:** крыша надстройки с мостиком (предложено).

## Шаг 1. Картинка-референс (ChatGPT или другой генератор картинок)

Вставьте текст целиком:

```text
A heavy missile battleship: a long, thick-armored hull with a low freeboard and a central superstructure holding the bridge. Two dual missile launchers sit fore and aft of the superstructure, each pod holding two upright missile tubes with rounded caps. Proportions: the hull is about five times longer than it is wide, and the superstructure is about one fifth of the hull length. Neutral static pose, isolated, with no water, no waves and no base. The team color zone is the flat roof panel of the bridge superstructure only.
heavy Eastern-bloc industrial design, boxy shapes
Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Шаг 2. Meshy (Image to 3D)

- Вход: картинка из шага 1.
- Настройки: PBR включить, если есть; лимит полигонов 8000.
- Текстовое уточнение (необязательно):

```text
A heavy missile battleship with a long armored hull, a central bridge superstructure and two dual missile launchers fore and aft. The team color is the flat magenta roof panel of the superstructure only.
```

## Шаг 2. Tripo (Image to 3D)

- Вход: та же картинка.
- Настройки: лимит полигонов 8000; PBR, если есть.
- Текстовое уточнение (необязательно):

```text
A heavy missile battleship with a long armored hull, a central bridge superstructure and two dual missile launchers fore and aft. The team color is the flat magenta roof panel of the superstructure only.
```

## Запасной вариант: только текст (Meshy или Tripo)

```text
A heavy missile battleship: a long, thick-armored hull with a low freeboard and a central superstructure holding the bridge. Two dual missile launchers sit fore and aft of the superstructure, each pod holding two upright missile tubes with rounded caps. Proportions: the hull is about five times longer than it is wide, and the superstructure is about one fifth of the hull length. Neutral static pose, isolated, with no water, no waves and no base. The team color zone is the flat roof panel of the bridge superstructure only.
heavy Eastern-bloc industrial design, boxy shapes
Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

## Проверка после генерации

- [ ] одна модель, без подставки, фона и текста
- [ ] корпус, надстройка и пусковые установки отделяются
- [ ] зоны команды одного цвета, других магентовых пятен нет
- [ ] полигонов не больше 8000
- [ ] силуэт читается под углом RA2 (проверить в Godot)
- [ ] похоже на картинку-референс и не копирует оригинал

---

_Источники: [appearance.md](appearance.md), карточка юнита, [generation_requirements.md](../../generation_requirements.md). Промты в генераторах не проверены._
