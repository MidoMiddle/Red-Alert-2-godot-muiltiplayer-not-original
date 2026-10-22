# 01. Колючая проволока: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Колючая проволока](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели секции проволоки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль нейтральный, без климата: ржавчина и снег добавляются текстурами. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `barbed_wire_section` | объект | 1x1 (одна секция) | 0,5 | ≤ 500 | пехоту и колёса |

Углы и концы делаем в Blender из секции, как у заборов ([08_fences_walls](../../map_elements/08_fences_walls/prompts.md)).

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/obstacles/`.

```text
Concept art reference for a barbed wire obstacle: two rows of steel barbed wire stretched between rusty wooden stakes, sharp barbs visible, bare ground at the base. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `barbed_wire_section`

```text
A straight section of barbed wire obstacle one unit long and half a unit tall: two rows of steel barbed wire stretched between rusty wooden stakes, with sharp barbs. Realistic physically based materials, clean readable silhouette, game-ready asset, neutral daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узлы: `wire` (проволока), `stakes` (колья).
- Колючки — упрощённая геометрия или альфа-карта: полная геометрия шипов дорогая.
- Секции стыкуются по краю клетки, углы и концы — из той же секции.
- Ржавчина и снег — текстуры климата, не модель.
- Цвета команды нет.
- Бюджет ≤ 500 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `barbed_wire_section.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
