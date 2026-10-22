# 02. Вертолётная площадка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Вертолётная площадка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `helipad` | здание | 3x3 | 1,5 (будка) | ≤ 8 000 | нет | да (полоса на крыше будки) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/infrastructure/`.

```text
Concept art reference for a helicopter landing pad: a square flat concrete pad with a large white letter H painted in the centre as the only marking, small yellow edge lights around it, a control hut with a flat roof in one corner, a striped orange and white windsock on a pole. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no words, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `helipad`

```text
A flat concrete helicopter landing pad three units square with a large white letter H painted in the centre and small yellow edge lights, a control hut about one and a half units tall at one corner with a flat roof, a striped windsock on a thin pole, a narrow colour band on the hut roof. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`.

## 4. Наши требования

- Узлы: `pad` (площадка), `hut` (будка), `windsock` (ветроуказатель), `lights` (огни), `team_mask` (полоса на крыше будки).
- Пивот в центре площадки на земле, высота 1,5.
- Текстуры 2048² (большое здание). Бюджет ≤ 8 000 треугольников.
- Буква H — часть текстуры площадки, не геометрия.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `helipad.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
