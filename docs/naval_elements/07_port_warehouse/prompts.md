# 07. Портовый склад: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Портовый склад](README.md) · [Требования к генерации](../../map_elements/generation_requirements.md)

Промты для картинки-референса и модели склада. Общие правила — в [требованиях](../../map_elements/generation_requirements.md). Стиль — климат «Побережье»: яркий летний дневной свет. Промты на английском.

---

## 1. Что нужно сделать

| Ассет | Тип | Футпринт | Высота (ориентир) | Бюджет LOD0 | Гарнизон | Цвет команды |
|---|---|---|---|---|---|---|
| `port_warehouse` | здание | 3x3 | 4 | ≤ 12 000 | да, 4 | да (полоса над воротами) |

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/naval/`.

```text
Concept art reference for a port warehouse on a harbour quay in bright summer daylight: a long grey metal hangar with a gabled roof, wide sliding gates on the front, a small two-storey office with windows at one end. Three-quarter view from 45 degrees, looking down at about 30 degrees. Isolated building on a plain light grey background, realistic materials, square image, no people, no text, no logos.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../../map_elements/generation_requirements.md), раздел 9.

### 3.1. `port_warehouse`

```text
A large port warehouse about three units wide, three units long and four units tall: a grey metal hangar with a gabled roof, wide sliding gates on the front, a small two-storey office with windows at one end, a narrow plain colour band above the gates. Realistic physically based materials, clean readable silhouette, game-ready asset, slightly exaggerated proportions like a late-1990s strategy game rebuilt in modern graphics, bright summer daylight, single isolated building on a plain light grey background.
```

Нормализация: `--kind building --footprint 3x3`. Ворота — отдельный узел `gate`.

## 4. Наши требования

- Узлы: `body` (ангар), `roof`, `office` (контора, гарнизон), `gate` (ворота), `team_mask` (полоса над воротами).
- Гарнизон 4: бойцы в конторе — код, окна видны в модели.
- Текстуры 2048² (большое здание). Бюджет ≤ 12 000 треугольников.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `port_warehouse.glb` | | | |

## Открытые вопросы

Открытых вопросов по модели нет.
