# 10. Ящики: промты для генерации

> Статус: черновик · Создан: 2026-10-22
> Родительский документ: [Ящики](README.md) · [Требования к генерации](../generation_requirements.md)

Промты для картинки-референса и модели ящика. Общие правила — в [требованиях](../generation_requirements.md). Промты на английском: генераторы с ним работают точнее.

---

## 1. Что нужно сделать

| Ассет | Футпринт | Высота (ориентир) | Бюджет LOD0 | Блокирует |
|---|---|---|---|---|
| `prop_supply_crate` | 1x1 (ящик меньше клетки) | 0,5 | ≤ 500 | нет |

Одна модель. Разные типы бонусов отличаются свечением и подписью в интерфейсе, а не формой ящика.

## 2. Картинка-референс (ChatGPT)

Вставить промт в ChatGPT как есть. Результат сохранить в `assets_src/concepts/crates/`.

```text
Concept art reference for a supply crate in a winter street: a wooden crate with light brown planks and metal corner brackets, a soft warm glow on its top edge, a few sparkles above it, snow on the ground. Three-quarter view from 45 degrees, looking down at about 30 degrees. Realistic materials, square image, no people, no text.
```

## 3. Модель (Meshy и Tripo)

Общие настройки сервисов — в [требованиях](../generation_requirements.md), раздел 9.

### 3.1. `prop_supply_crate`

```text
A wooden supply crate about half a unit wide, light brown planks, metal corner brackets and two metal straps, slightly weathered, closed lid. Realistic physically based materials, clean readable silhouette, game-ready asset, cold winter setting, single isolated object on a plain light grey background.
```

Нормализация: `--kind prop --footprint 1x1`.

## 4. Наши требования

- Свечение ящика — emissive-материал в Godot, в модели и текстуре его нет.
- Ящик меньше клетки. Пивот — центр клетки, низ на земле.
- Ящик не блокирует проход. Цвет команды не нужен.
- Ящик должен выделяться над декором: свечение и покачивание, а не форма. Форма держится простой, чтобы её легко было читать на расстоянии.

## 5. Реестр

| Файл | Сервис | Дата | Статус |
|---|---|---|---|
| `prop_supply_crate.glb` | | | |

## Открытые вопросы

1. Показывать ли тип бонуса до подбора (значок над ящиком или цвет свечения).
2. Нужен ли ящик, который прилетает на парашюте: тогда нужна отдельная модель парашюта.
