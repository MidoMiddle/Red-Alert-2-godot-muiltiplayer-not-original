# 10. Отдыхающие: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Отдыхающие](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и пляжной одежды. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет, сухо. Промты на английском.

---

## 1. Что нужно сделать

Новых моделей не нужно. Используем мирных жителей из [13_civilians](../../map_elements/13_civilians/prompts.md) (`civ_man`, `civ_woman`) и меняем одежду и позу:

| Ассет | Основа | Что меняем | Бюджет LOD0 |
|---|---|---|---|
| `civ_beach_man` | `civ_man` | одежда: шорты, футболка | ≤ 1 500 |
| `civ_beach_woman` | `civ_woman` | одежда: сарафан или купальник | ≤ 1 500 |
| `civ_beach_prone` | `civ_man` | поза лёжа (клип Mixamo `prone`) | — (та же модель) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/beach/`.

```text
Concept art reference for beachgoers in bright summer daylight: a man in shorts and a bright t-shirt sitting on a towel, a woman in a sundress with a sun hat standing near a striped umbrella, another person lying on a towel on the sand. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, plain light grey background, square image, no text, no logos.
```

## 3. Перекраска одежды (Meshy или Tripo)

Берём готовую модель из 13, перекрашиваем по тексту. Если сервис умеет перекраску, это быстрее новой генерации. Если не умеет, делаем новую модель в T-позе.

### 3.1. `civ_beach_man` (перекраска `civ_man`)

```text
The same man, now in beach clothes: bright blue shorts and a white short-sleeved t-shirt, sandals, no hat, no weapons, no uniform. Realistic physically based materials, keep the same body and face.
```

### 3.2. `civ_beach_woman` (перекраска `civ_woman`)

```text
The same woman, now in beach clothes: a yellow sundress and a wide-brimmed sun hat, sandals, no weapons, no uniform. Realistic physically based materials, keep the same body and face.
```

Нормализация: как у `civ_man` и `civ_woman` — `--kind prop --footprint 1x1` (уточнить в M1, см. [13_civilians](../../map_elements/13_civilians/prompts.md)).

### 3.3. `civ_beach_prone` (поза)

Не генерируем. Берём клип Mixamo `prone` на модели `civ_beach_man`, как у солдатов в M1 (4.13). Лицевая сторона — на песке.

## 4. Наши требования

- Одежда — варианты текстуры. Одна модель на пол, а не на каждую расцветку.
- Скелет — Mixamo (M1, 4.13). Клипы `idle`, `walk`, `run`, `prone`, `die_0..2`.
- Без военной формы, без оружия, без крови.
- Цвета команды нет: люди нейтральные.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `civ_beach_man` (перекраска) | | | |
| `civ_beach_woman` (перекраска) | | | |

Клип `prone` — из библиотеки Mixamo, лицензию фиксируем в реестре (M1, 4.13).

## Открытые вопросы

1. Если сервис не умеет перекраску, нужна ли отдельная модель в купальнике.
