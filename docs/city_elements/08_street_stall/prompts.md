# 08. Уличная лавка: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Уличная лавка](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели уличной лавки. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_street_stall` | объект | 1x1 | 1,2 | ≤ 800 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a street vendor stall on a city pavement in neutral daylight: a small cart on wheels with a striped umbrella above it, coloured goods on display, a low counter. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_street_stall`

```text
A street vendor cart on wheels with a striped umbrella on a pole above it, a wooden or metal body, a low counter with coloured goods on display, no people. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `cart` (тележка), `umbrella` (зонтик, покачивается в коде отдельно от тележки).
- **Гарнизона нет.**
- **Блокирование** — только для техники. Это данные карты ([07](../07_kiosk/prompts.md), [требования](../../map_elements/generation_requirements.md)), в модели не отражается.
- **Товары** — простые цветные формы. Не делаем узнаваемые продукты, чтобы не копировать реальные бренды.
- **Бюджет** ≤ 800 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_street_stall.glb` | Blender, скрипт `tools/blender/families/city_street_stall.py` | 2026-10-29 | готово |
| `city_street_stall_damaged.glb` | то же | 2026-10-29 | готово |
| `city_street_stall_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Три состояния по [appearance.md](appearance.md): целая, повреждённая (зонтик порван, товары рассыпаны) и разрушенная (тележка на боку). Узлы `cart` и `umbrella`. Товары — простые цветные кучки, без узнаваемых продуктов. Высота не 1,2, а 0,84 до верха зонтика: объекты карты игра не сжимает по высоте, и при 1,2 лавка стояла бы выше сжатых домов. В игре лавка — объект карты `street_stall` (`game/data/map/objects.yaml`); целая модель подключена к нему в `game/data/visuals/models.yaml`. Состояния объектов карты клиент пока не показывает, поэтому повреждённая и разрушенная модели ждут этого.

## Открытые вопросы

1. Нужны ли разные варианты лавок (фрукты, газеты) или хватит одного.
