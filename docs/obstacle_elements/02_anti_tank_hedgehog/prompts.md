# 02. Противотанковый еж: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Противотанковый еж](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели ежа. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль нейтральный, без климата: ржавчина и песок или снег добавляются текстурами. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|---|
| `anti_tank_hedgehog` | объект | 1x1 | 0,5 | ≤ 800 | гусеницы и колёса |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/obstacles/`.

```text
Concept art reference for an anti-tank hedgehog obstacle: three steel beams welded together into a three-dimensional star, sharp ends touching the ground, dark grey steel with rust at the welds. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `anti_tank_hedgehog`

```text
A steel anti-tank hedgehog obstacle about one unit across and half a unit tall: three steel beams welded together into a three-dimensional star, sharp ends, dark grey steel with some rust at the welds, no markings. Realistic physically based materials, clean readable silhouette, game-ready asset, neutral daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Пивот — центр клетки на земле. Узел: `body`.
- Блокирует гусеницы и колёса, пехота проходит. Это данные карты, в модели не отражается.
- Ржавчина — текстура. Цвета команды нет.
- Бюджет ≤ 800 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `anti_tank_hedgehog.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
