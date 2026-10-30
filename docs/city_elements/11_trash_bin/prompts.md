# 11. Мусорный контейнер: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Мусорный контейнер](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели мусорного контейнера. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_trash_bin` | объект | 1x1 | 0,6 | ≤ 500 | нет | нет |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a metal waste container by a city building entrance in neutral daylight: a dark green galvanised container with a hinged lid on small wheels, a little rust at the corners. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_trash_bin`

```text
A metal waste container with a hinged lid on small wheels, dark green galvanised steel, a little rust at the corners, about one unit long and six tenths of a unit tall. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- **Пивот** — центр клетки на земле. Узлы: `body`, `lid` (крышка, открывается анимацией при желании).
- **Не блокирует проход**, укрытием не является (см. [appearance.md](appearance.md)).
- **Горение** — дым и обугливание крышки в коде. Модель одна, без состояний.
- **Бюджет** ≤ 500 треугольников, текстуры 1024².

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_trash_bin.glb` | Blender, скрипт `tools/blender/families/city_trash_bin.py` | 2026-10-29 | готово |
| `city_trash_bin_damaged.glb` | то же | 2026-10-29 | готово |
| `city_trash_bin_ruins.glb` | то же | 2026-10-29 | готово |

Модель сделана скриптом, без генератора. Кроме основной модели сделаны два состояния по [appearance.md](appearance.md): повреждённый (вмятина, больше ржавчины, половина крышки откинута, мешок рядом) и разрушенный (контейнер на боку, крышка на земле, мешки и бумага вокруг). Горение — дым и обугливание в коде, как здесь и сказано. Узлы `body` и `lid`. Размер не «одна клетка в длину и 0,6 в высоту», а 0,7 в длину и 0,48 в высоту с крышкой: в том размере, в котором контейнер рисует игра, чуть больше скамейки. В игре контейнер — объект карты `trash_bin` (`game/data/map/objects.yaml`); основная модель подключена к нему в `game/data/visuals/models.yaml`. Состояния объектов карты клиент пока не показывает.

## Открытые вопросы

Открытых вопросов по модели нет: элемент декоративный.
