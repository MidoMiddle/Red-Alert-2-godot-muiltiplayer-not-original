# 12. Почтовый ящик: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Почтовый ящик](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели почтового ящика. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_mailbox` | объект | 1x1 | 0,6 | ≤ 500 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a blue steel mailbox with a hinged flap on a short concrete pedestal by a city pavement in neutral daylight. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_mailbox`

```text
A blue steel mailbox with a hinged flap and a slot, on a short concrete pedestal, a plain white label area with no text, about six tenths of a unit tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `body`, `flap` (крышка, отдельным узлом для анимации открытия).
- **Не блокирует проход.** Цвета команды нет.
- **Номер дома** на ящике — текстура или без надписей. Текст не делаем.
- **Бюджет** ≤ 500 треугольников, текстуры 512² (малый объект).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_mailbox.glb` | Blender, скрипт `tools/blender/families/city_mailbox.py` | 2026-10-29 | готово |
| `city_mailbox_damaged.glb` | то же | 2026-10-29 | готово |
| `city_mailbox_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Три состояния по [appearance.md](appearance.md): целый, повреждённый (крышка погнута, замок выбит, краска содрана) и разрушенный (ящик на земле, от тумбы остался обрубок). Узлы `body` и `flap`. Высота 0,55, около полклетки, в размере, в котором ящик рисует игра. Номера дома и надписей нет: белая табличка пустая. В игре ящик — объект карты `mailbox` (`game/data/map/objects.yaml`); целая модель подключена к нему в `game/data/visuals/models.yaml`. Состояния объектов карты клиент пока не показывает.

## Открытые вопросы

Открытых вопросов по модели нет: элемент декоративный.
