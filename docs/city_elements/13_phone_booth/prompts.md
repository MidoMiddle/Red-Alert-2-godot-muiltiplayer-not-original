# 13. Телефонная будка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Телефонная будка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели телефонной будки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_phone_booth` | объект | 1x1 | 1,5 | ≤ 1 000 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a glass telephone booth on a city pavement in neutral daylight: a dark red metal frame, transparent glass panels with light reflections, a small roof overhang, a phone unit visible inside. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_phone_booth`

```text
A glass telephone booth on a metal frame with a dark red painted frame, transparent glass panels, a small roof overhang, and a phone unit on the inside wall, about one and a half units tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `body` (каркас), `glass` (панели).
- **Стекло** — отдельный прозрачный материал. Генераторы с прозрачностью работают плохо, поэтому материал задаём после генерации.
- **Блокирует проход** — это данные карты ([appearance.md](appearance.md)).
- **Гарнизона нет.** Внутри никто не прячется.
- **Бюджет** ≤ 1 000 треугольников, текстуры 512².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_phone_booth.glb` | Blender, скрипт `tools/blender/families/city_phone_booth.py` | 2026-10-29 | готово |
| `city_phone_booth_damaged.glb` | то же | 2026-10-29 | готово |
| `city_phone_booth_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Три состояния по [appearance.md](appearance.md): целая, повреждённая (двух панелей нет, дверь перекошена, оставшееся стекло мутнее) и разрушенная (рама наклонилась, стекла нет, крыша на земле, осколки). Узлы `body` (каркас, телефон, табличка без текста, лампа) и `glass` (панели). Стекло — отдельный прозрачный материал, не запекается; в GLB он полупрозрачный (`alphaMode: BLEND`). Высота не 1,5, а 0,8 до верха крыши, в размере, в котором будку рисует игра: при 1,5 она стояла бы выше стен сжатых домов. В игре будка — объект карты `phone_booth` (`game/data/map/objects.yaml`); целая модель подключена к нему в `game/data/visuals/models.yaml`. Состояния объектов карты клиент пока не показывает.

## Открытые вопросы

1. Блокирует ли будка пехоту целиком или только технику (дверь открыта, но пехота в неё не заходит).
