# 04. Офисное здание: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Офисное здание](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели офиса. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_office` | здание | 2x2 | 6 | ≤ 10 000 | да, 5 | да (полоса по краю крыши) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a tall glass-and-concrete office building on a central city square in neutral daylight: horizontal window bands, a wide glass shopfront at ground level with a plain sign board, a flat roof with small antennas. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_office`

```text
A tall glass-and-concrete office building with a plain facade of horizontal window bands, a ground floor with a wide glass shopfront and a plain sign board without text, and a flat roof with two small antennas and a narrow colour band along the roof edge. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 2x2`.

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `body`, `roof`, `team_mask` (полоса по краю крыши).
- **Высота 6** — выше или вровень с многоэтажкой ([02](../02_apartment_block/prompts.md)). Офис — самое высокое здание центра, пока на карте нет небоскрёба ([19](../19_skyscraper/README.md)). Это правило из [appearance.md](appearance.md).
- **Стекло** — отдельный материал (`glass`), прозрачность задаём в Blender или Godot. Генераторы с прозрачностью работают плохо.
- **Гарнизон 5.** Бойцы в верхних окнах — код.
- **Бюджет** ≤ 10 000 треугольников, текстуры 2048².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_office.glb` | Blender, скрипт `tools/blender/families/city_office.py` | 2026-10-29 | готово |
| `city_office_occupied.glb` | то же | 2026-10-29 | готово |
| `city_office_damaged.glb` | то же | 2026-10-29 | готово |
| `city_office_damaged_occupied.glb` | то же | 2026-10-29 | готово |
| `city_office_abandoned.glb` | то же | 2026-10-29 | готово |
| `city_office_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Высота до крыши 6,05, с парапетом 6,25: выше многоэтажки. Цвет владельца — парапет по краю крыши. Стекло запечено в общую текстуру и непрозрачное: отдельный прозрачный материал не делали. В игре офис — `neutral_office` (`game/data/neutral/civilian.yaml`); модели подключены к нему в `game/data/visuals/models.yaml` со всеми состояниями. В данных вместимость гарнизона 6, а в модели стволов 5, по этому документу.

## Открытые вопросы

1. Сделать ли офис спорной точкой, как техздание, с бонусом владельцу. Сейчас бонуса нет, и модель от этого не меняется.
