# Генерация моделей юнитов: требования и порядок работы

> Статус: черновик · Создан: 2026-10-22
> Связанные документы: [справочник юнитов](README.md), [M1_visual_spike.md](../implementation/M1_visual_spike.md), [09_graphics_art.md](../09_graphics_art.md), [15_legal.md](../15_legal.md)

Общие правила для промтов. Каждый юнит имеет свой `prompts.md` в папке юнита, там промты конкретного юнита. Требования в них одинаковые. Если правило меняется здесь, его меняем и в промтах.

Важно: проба M1 приостановлена, внешность делаем на финальном этапе ([16_roadmap.md](../16_roadmap.md)). Всё ниже — заготовка, которую проверим в пробе генераторов.

---

## 1. Порядок работы

1. **Картинка-референс.** Генератор картинок (ChatGPT или другой) по промту из шага 1 файла юнита. Картинка задаёт форму, её сохраняем.
2. **3D.** Картинку загружаем в Meshy или Tripo (Image to 3D). Скачиваем модель, лучше `.glb`.
3. **Чистка.** Blender-скрипты из командной строки: масштаб, оси, точка опоры, отделение башни, маска цвета команды, LOD, экспорт `.glb` ([M1, раздел 4.8](../implementation/M1_visual_spike.md)).
4. **Проверка.** Отчёт `rts_validate.py` и осмотр в Godot под углом RA2.
5. **Импорт.** `game/assets/models/<kind>/<id>/`, запись в `docs/assets_registry.md`.

---

## 2. Технические требования (вставляются в каждый промт)

```text
Technical requirements: one single object, centered, standing on nothing: no base, no ground plane, no background, no text, no logos, no labels. Neutral static pose; infantry in T-pose with arms straight out to the sides. Separate parts where the design has them: hull, turret, barrel. Team color zones are flat pure magenta (#FF00FF) and appear only where the unit description says; no other magenta anywhere. Realistic PBR materials, no baked shadows or lighting in the textures. Clean closed geometry, no loose or floating pieces. Proportions as described; a readable silhouette matters more than small details.
```

---

## 3. Стиль

```text
Style: realistic 3D military design for a real-time strategy game, detailed but simple shapes, readable from a distance, saturated but natural colors, warm lighting. No text, logos or emblems.
```

Строка стиля фракции (добавляется в промт шага 1):

- **Союзники:** `modern Western military design, clean angular shapes`.
- **Советы:** `heavy Eastern-bloc industrial design, boxy shapes`.

---

## 4. Картинка-референс (шаг 1)

```text
Image: three-quarter view from above, about 40 degrees, the whole unit centered and fully visible, even soft studio lighting, neutral light-gray background, no environment, no text, no logos.
```

---

## 5. Настройки генераторов

- **Meshy** (Image to 3D): включить PBR, если есть. Лимит полигонов по таблице ниже. Текстовое уточнение необязательно.
- **Tripo** (Image to 3D): то же.
- Названия полей в интерфейсах меняются, их проверяем в самих сервисах.
- Тариф должен разрешать распространять результат в бесплатной публичной игре. Условия и цену проверяем до покупки ([M1, раздел 4.9](../implementation/M1_visual_spike.md)).

| Тип юнита | Лимит полигонов (LOD0) | Источник |
|---|---|---|
| Пехота | 1 500 | [M1, раздел 4.7](../implementation/M1_visual_spike.md) |
| Техника | 8 000 | [M1, раздел 4.7](../implementation/M1_visual_spike.md) |
| Авиация и флот | 8 000 | предложение, в M1 не задано |

---

## 6. Правила для промтов

- Промт на английском: генераторы с ним работают лучше. Пояснения в файлах на русском.
- В английских блоках промта нет оригинальных названий юнитов, игры и фирм (Rhino, Grizzly, Tanya, Red Alert, RA2 и т. п.). Юнит описываем ролью и формой.
- Не копируем конкретную модель: описываем форму, роль и детали, а не референс. Так требует [15_legal.md](../15_legal.md).
- Цвета в промтах, которых нет в справочнике, — наше предложение. В источниках они не подтверждены.
- Техника, авиация и флот: без экипажа и пилотов. Флот: без воды, волн и основания.

---

## 7. Что предложено, а не решено

| Вопрос | Предложение | Как проверить |
|---|---|---|
| Цвет команды | Чистый пурпурный `#FF00FF`, одна зона на нужную часть | В пробе: маска через `rts_team_mask` |
| Имя файла модели | `id` из кода для уже существующих типов (`heavy_tank`), для остальных собственные имена | Имена не выбраны (открытый вопрос 05): папки названы по оригиналу, в игру их не переносим |
| Пехота | T-поза, чтобы потом разметить в Mixamo | Проба M1, раздел 4.13 |
| Палитра юнитов | Предложена нами, в источниках не подтверждена | Решить вместе с «арт-библией» ([09_graphics_art.md](../09_graphics_art.md), открытые вопросы) |
