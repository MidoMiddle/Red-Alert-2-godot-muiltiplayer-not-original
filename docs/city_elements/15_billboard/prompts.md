# 15. Рекламный щит: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Рекламный щит](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса, модели щита и постера. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Город»: нейтральный дневной свет, без снега. Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `city_billboard` | объект | 2x1 | 2,2 (низ панели 1,0) | ≤ 3 000 | нет | нет |
| `poster_billboard` | текстура, 2:1 | 2048×1024 | — | — | — | нет |

Постер — отдельная картинка. Так один щит может показывать разные рекламные образы без новой модели.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/city/`.

```text
Concept art reference for a large billboard on two steel posts beside a city road in neutral daylight: a plain grey frame, a large flat advertising panel with bold graphics and no readable text, the bottom edge above head height. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated object on a plain light grey background, soft even daylight, dry weather, realistic materials, square image, no people, no logos.
```

## 3. Модель и постер

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `city_billboard` (Meshy и Tripo)

```text
A large rectangular billboard panel on two steel posts, a plain grey frame around a flat panel face, the bottom edge of the panel about one unit above the ground, no text on the panel. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, dry neutral city daylight, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 2x1`. В Blender отметить панель узлом `sign_face`: на неё накладываем постер.

### 3.2. `poster_billboard` (генератор изображений)

```text
Flat poster artwork for a fictional brand: bold red and yellow shapes, one simple white icon in the centre, a plain background, no legible text, no real logos, 2:1 aspect ratio, sharp edges.
```

Вымышленный бренд. Реальных марок и логотипов не используем ([15_legal](../../15_legal.md), раздел 3).

## 4. Наши требования

- **Пивот** — центр футпринта на земле. Узлы: `posts` (опоры), `sign_face` (панель с постером).
- **Блокирует только опоры** — по одной клетке каждая. Щит над клеткой не блокирует, это данные карты.
- **Текст на постере не делаем** (только символы). Это избавляет от зависимости от языка.
- **Щит на фасаде** — отдельный вариант, пока открытый вопрос: сейчас модель стоит на опорах.
- **Бюджет** ≤ 3 000 треугольников (средний объект), текстуры 1024² (постер 2048×1024).

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `city_billboard.glb` | Blender, скрипт `tools/blender/families/city_billboard.py` | 2026-10-29 | готово |
| `city_billboard_damaged.glb` | то же | 2026-10-29 | готово |
| `city_billboard_ruins.glb` | то же | 2026-10-29 | готово |
| `poster_billboard.png` | | | не нужен пока: постер нарисован материалом |

Модель сделана скриптом, без генератора. Три состояния по [appearance.md](appearance.md): целый, повреждённый (верхний правый угол оторван, видна подложка, постер в трещинах и пятнах) и разрушенный (панель лежит на земле, опоры согнуты). Узлы `posts` (опоры, рама, задняя сторона) и `sign_face` (лицевая панель). Постер вымышленного бренда нарисован материалом и запечён в текстуру: красный фон, жёлтая диагональ, белый круг с синим квадратом, без текста и реальных знаков. Отдельный `poster_billboard.png` пока не нужен; если понадобятся разные постеры, их можно положить на узел `sign_face`. Размер не 2×1 и не 2,2 в высоту, а одна клетка (0,98 в ширину) и 0,98 до верха панели, низ панели на 0,5 — выше пехоты: объект карты стоит на одной клетке, и в том размере, в котором его рисует игра. В игре щит — объект карты `billboard` (`game/data/map/objects.yaml`); целая модель подключена к нему в `game/data/visuals/models.yaml` без случайного поворота. Состояния объектов карты клиент пока не показывает.

## Открытые вопросы

1. Нужны ли разные картинки щитов для разнообразия улиц (несколько постеров).
2. Щит на фасаде — отдельный элемент или декаль здания.
