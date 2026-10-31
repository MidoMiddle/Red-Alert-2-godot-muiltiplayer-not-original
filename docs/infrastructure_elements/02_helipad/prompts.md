# 02. Вертолётная площадка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Вертолётная площадка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — нейтральный дневной свет, без климата. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `helipad` | здание | 3x3 | 0,69 (будка), 1,2 (мачта) | ≤ 8 000 | нет | да (парапет крыши будки) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/infrastructure/`.

```text
Concept art reference for a helicopter landing pad: a square flat concrete pad with a large white letter H painted in the centre as the only marking, small yellow edge lights around it, a control hut with a flat roof in one corner, a striped orange and white windsock on a pole. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, realistic materials, neutral daylight, square image, no people, no words, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `helipad`

```text
A flat concrete helicopter landing pad three units square with a large white letter H painted in the centre and small yellow edge lights, a low control hut with a mast at one corner with a flat roof, a striped windsock on a thin pole, a narrow colour band on the hut roof. Realistic physically based materials, clean readable silhouette, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, neutral daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`.

## 4. Наши требования

- Узлы: `pad` (площадка), `hut` (будка), `windsock` (ветроуказатель), `lights` (огни), `team_mask` (полоса на крыше будки).
- Пивот в центре площадки на земле. Размер игры, без сжатия, как у аэродрома техзданий: будка до 0,69, мачта до 1,2.
- Пивот узла `windsock` — на оси шеста, игра поворачивает его по ветру. У разрушенной — узлы `pad` и `body`.
- Текстуры 2048² (большое здание). Бюджет ≤ 8 000 треугольников.
- Буква H — часть текстуры площадки, не геометрия.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `helipad.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/infrastructure_helipad.py`) | 2026-10-31 | сделано, 2 826 треугольников |
| `helipad_damaged.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/infrastructure_helipad.py`) | 2026-10-31 | сделано, 3 110 треугольников |
| `helipad_ruins.glb` | Blender 5.2.3, вручную (скрипт `tools/blender/families/infrastructure_helipad.py`) | 2026-10-31 | сделано, 1 236 треугольников |

## Открытые вопросы

Открытых вопросов по модели нет.
