// Собирает список звуков игры для страницы прослушки (index.html рядом). Запуск из корня репозитория:
//   node tools/audio-picker/build.mjs
//
// Откуда что берётся:
//   game/data/factions, game/data/neutral — юниты и здания: их имена, оружие;
//   game/data/audio/sounds.yaml           — звуки игры и их файлы (game/assets/audio);
//   game/data/audio/bindings.yaml          — какое оружие, здание, мост, летун чем звучит;
//   assets_src/audio/takes/<звук>/*.mp3    — дубли из ElevenLabs, takes.json рядом: [{file, prompt, seed, date, tier}].
// Место под звук, у которого ещё нет своего (голоса, шаги, штаб, окружение), получает запланированное имя: vo_…, eva_…, amb_…;
// дубли для него кладутся в takes/<это имя>/.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../..');
const data = path.join(root, 'game', 'data');
const audioDir = path.join(root, 'game', 'assets', 'audio');
const takesDir = path.join(root, 'assets_src', 'audio', 'takes');
const AUDIO = /\.(wav|mp3|ogg|m4a|flac)$/i;

// ---- YAML данных игры: таблицы с отступами, значения в строку, [списки] и {таблицы} в строку; этого хватает для game/data.
function parseYaml(text) {
  const top = {};
  const stack = [{indent: -1, node: top}];
  for (const raw of text.split('\n')) {
    const line = raw.replace(/\s+#.*$/, '').replace(/^#.*$/, '');
    if (!line.trim()) continue;
    const m = line.match(/^(\s*)([A-Za-z0-9_@.-]+):\s*(.*)$/);
    if (!m) continue;
    const indent = m[1].length;
    while (stack.at(-1).indent >= indent) stack.pop();
    const parent = stack.at(-1).node;
    if (m[3] === '') {
      parent[m[2]] = {};
      stack.push({indent, node: parent[m[2]]});
    } else {
      parent[m[2]] = scalar(m[3]);
    }
  }
  return top;
}

function scalar(text) {
  const t = text.trim();
  if (t.startsWith('[') && t.endsWith(']')) return t.slice(1, -1).split(',').map((s) => scalar(s)).filter((s) => s !== '');
  if (t.startsWith('{') && t.endsWith('}')) {
    const out = {};
    for (const pair of t.slice(1, -1).split(',')) {
      const [k, ...v] = pair.split(':');
      if (k.trim()) out[k.trim()] = scalar(v.join(':'));
    }
    return out;
  }
  return t.replace(/^["']|["']$/g, '');
}

const readYaml = (file) => parseYaml(fs.readFileSync(file, 'utf8'));
const yamlFiles = (dir) =>
  fs.readdirSync(dir, {withFileTypes: true}).flatMap((e) =>
    e.isDirectory() ? yamlFiles(path.join(dir, e.name)) : e.name.endsWith('.yaml') ? [path.join(dir, e.name)] : [],
  );

// Все определения данных по id (kind: unit, building, map_building, weapon…)
const defs = {};
for (const file of yamlFiles(data)) {
  if (file.includes(`${path.sep}audio${path.sep}`) || file.includes(`${path.sep}maps${path.sep}`)) continue;
  for (const [id, fields] of Object.entries(readYaml(file))) {
    if (fields && typeof fields === 'object' && fields.kind) defs[id] = {id, ...fields, source: path.relative(root, file)};
  }
}

const sounds = readYaml(path.join(data, 'audio', 'sounds.yaml')).sounds ?? {};
const bindings = readYaml(path.join(data, 'audio', 'bindings.yaml'));

// ---- Файлы: звук в работе (из sounds.yaml) и дубли
const href = (file) => path.relative(here, file).split(path.sep).map(encodeURIComponent).join('/');
const byNumber = (a, b) => a.localeCompare(b, 'ru', {numeric: true});
const readJson = (file) => {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
};

function filesOf(sound) {
  const items = [];
  for (const f of [].concat(sounds[sound]?.files ?? [])) {
    const file = path.join(audioDir, f);
    if (fs.existsSync(file)) items.push({file: path.relative(root, file), src: href(file), current: true});
  }
  const dir = path.join(takesDir, sound);
  if (fs.existsSync(dir)) {
    const meta = new Map((readJson(path.join(dir, 'takes.json')) ?? []).map((m) => [m.file, m]));
    for (const name of fs.readdirSync(dir).filter((n) => AUDIO.test(n)).sort(byNumber)) {
      const file = path.join(dir, name);
      const m = meta.get(name) ?? {};
      items.push({
        file: path.relative(root, file),
        src: href(file),
        current: false,
        prompt: m.prompt ?? null,
        seed: m.seed ?? null,
        date: m.date ?? null,
        tier: m.tier ?? null,
      });
    }
  }
  return items;
}

// ---- Каталог: категория → разделы → карточки → места под звук
const slot = (label, sound) => ({label, sound});
const shotOf = (weapon) => (weapon ? bindings.weapons?.[weapon] ?? 'snd_shot' : null);
const nameOf = (d) => d.name ?? d.id;

const FACTIONS = {alliance: 'Альянс', union: 'Союз'};
// Поле с учётом шаблона (inherits), как его читает игра
const field = (d, key) => (d[key] !== undefined ? d[key] : d.inherits && defs[d.inherits] ? field(defs[d.inherits], key) : undefined);
const roots = (d) => (d.inherits && defs[d.inherits] ? [d.inherits, ...roots(defs[d.inherits])] : []);
const unitKind = (u) =>
  field(u, 'category') === 'aircraft' ? 'Авиация' : field(u, 'category') === 'naval' ? 'Флот' : roots(u).includes('base_infantry') ? 'Пехота' : 'Техника';

// Мирных не выбирают и не ведут: у них только движение и гибель
function unitCard(u, civilian = false) {
  const slots = [];
  for (const [key, label] of [['primary', 'Выстрел'], ['secondary', 'Второе оружие'], ['deployed_weapon', 'Выстрел развёрнутым']]) {
    const weapon = field(u, key);
    if (weapon) slots.push(slot(`${label} · ${weapon}`, shotOf(weapon)));
  }
  slots.push(slot('Движение', bindings.movement?.[u.id] ?? `snd_move_${u.id}`));
  slots.push(slot('Гибель', 'snd_unit_death'));
  if (!civilian) {
    slots.push(slot('Голос: выбор', `vo_${u.id}_select`), slot('Голос: приказ движения', `vo_${u.id}_move`));
    slots.push(slot('Голос: приказ атаки', `vo_${u.id}_attack`));
  }
  // Сторона и тип — для фильтров страницы
  return {id: u.id, title: nameOf(u), side: civilian ? 'Мирные' : FACTIONS[field(u, 'faction')] ?? field(u, 'faction'), type: unitKind(u), slots};
}

function buildingCard(b) {
  const slots = [slot('Обрушение', bindings.buildings?.[b.id] ?? bindings.buildings?.default ?? null)];
  if (b.primary) slots.push(slot(`Выстрел · ${b.primary}`, shotOf(b.primary)));
  const side = FACTIONS[b.faction] ?? (b.id.startsWith('tech_') ? 'Технологические' : 'Гражданские');
  return {id: b.id, title: nameOf(b), side, type: b.primary ? 'Оборона' : 'Постройки', slots};
}

const all = Object.values(defs);
// Шаблоны (base_…) — не юниты; мирные жители и их машины — свой раздел
const units = all.filter((d) => d.kind === 'unit' && !d.id.startsWith('base_') && field(d, 'faction'));
const civilians = all.filter((d) => d.kind === 'unit' && !d.id.startsWith('base_') && !field(d, 'faction'));
const sectionsBy = (items, keyOf, order) => {
  const map = new Map();
  for (const it of items) {
    const key = keyOf(it);
    if (!map.has(key)) map.set(key, []);
    map.get(key).push(it);
  }
  return [...map.entries()].sort((a, b) => order(a[0]) - order(b[0]) || byNumber(a[0], b[0]));
};

const KINDS = ['Пехота', 'Техника', 'Авиация', 'Флот'];
const unitSections = sectionsBy(
  units,
  (u) => `${FACTIONS[field(u, 'faction')] ?? field(u, 'faction')} · ${unitKind(u)}`,
  (key) => Object.values(FACTIONS).indexOf(key.split(' · ')[0]) * 10 + KINDS.indexOf(key.split(' · ')[1]),
)
  .map(([title, list]) => ({title, cards: list.map((u) => unitCard(u))}))
  .concat(civilians.length ? [{title: 'Мирные жители, животные, машины', cards: civilians.map((u) => unitCard(u, true))}] : []);

const buildingSections = [
  ...Object.entries(FACTIONS).map(([id, title]) => ({
    title,
    cards: all.filter((d) => d.kind === 'building' && d.faction === id).map(buildingCard),
  })),
  {title: 'Гражданские здания карты', cards: all.filter((d) => d.kind === 'map_building' && d.id.startsWith('neutral_')).map(buildingCard)},
  {title: 'Технологические здания', cards: all.filter((d) => d.kind === 'map_building' && d.id.startsWith('tech_')).map(buildingCard)},
].filter((s) => s.cards.length);

// События, мир и меню: списком; запланированное — то, чего в игре ещё нет
const list = (title, entries) => ({title, cards: entries.map(([id, name, sound, note]) => ({id, title: name, note, slots: [slot(name, sound)]}))});

const eventSections = [
  list('Бой', [
    ['impact', 'Попадание без разлёта', 'snd_impact'],
    ['explosion', 'Взрыв', 'snd_explosion'],
    ['prism_support', 'Вспомогательный луч башни', 'snd_prism_support'],
    ['prism_tank_charge', 'Зарядка призма-танка', 'snd_prism_tank_charge', 'в игре нет события начала зарядки'],
    ['prism_tower_charge', 'Зарядка призменной башни', 'snd_prism_tower_charge', 'в игре нет события начала зарядки'],
    ['jet_flyby', 'Пролёт истребителя', 'snd_jet_flyby', 'не подключён'],
  ]),
  list('База', [
    ['place', 'Здание поставлено', 'snd_building_place'],
    ['sell', 'Здание продано', 'snd_building_sell'],
    ['repair', 'Ремонт', 'snd_building_repair'],
    ['power_down', 'Отключение энергии', 'snd_power_down'],
  ]),
  list('Штаб (EVA)', [
    ['construction_complete', '«Строительство завершено»', 'eva_construction_complete'],
    ['unit_ready', '«Юнит готов»', 'eva_unit_ready'],
    ['training', '«Обучение»', 'eva_training'],
    ['insufficient_funds', '«Недостаточно средств»', 'eva_insufficient_funds'],
    ['low_power', '«Мало энергии»', 'eva_low_power'],
    ['base_under_attack', '«База атакована»', 'eva_base_under_attack'],
    ['unit_lost', '«Юнит потерян»', 'eva_unit_lost'],
    ['building_lost', '«Здание потеряно»', 'eva_building_lost'],
    ['new_options', '«Новые возможности строительства»', 'eva_new_options'],
    ['superweapon_ready', '«Супероружие готово»', 'eva_superweapon_ready'],
  ]),
];

const worldSections = [
  list('Мосты', [
    ['bridge_wood_span', 'Пролёт деревянного моста', bindings.bridges?.bridge_wood_low?.span],
    ['bridge_wood_all', 'Весь деревянный мост (C4)', bindings.bridges?.bridge_wood_low?.whole],
    ['bridge_concrete', 'Высокий бетонный мост', bindings.bridges?.bridge_concrete_high?.whole],
  ]),
  list('Окружение', [
    ['wind_winter', 'Ветер, зима', 'amb_wind_winter'],
    ['wind_desert', 'Ветер, пустыня', 'amb_wind_desert'],
    ['water', 'Вода у берега', 'amb_water_shore'],
    ['birds', 'Птицы, лето', 'amb_birds'],
    ['city', 'Город вдали', 'amb_city'],
    ['rain', 'Дождь', 'amb_rain'],
    ['storm', 'Гроза', 'amb_thunder'],
  ]),
  list('Объекты карты', [
    ['crate', 'Ящик подобран', 'snd_crate_pickup'],
    ['tree', 'Дерево падает', 'snd_tree_fall'],
    ['ore', 'Сбор руды', 'snd_ore_harvest'],
  ]),
];

const menuSections = [
  list('Кнопки и элементы', [
    ['hover', 'Наведение', 'snd_ui_hover'],
    ['click', 'Нажатие', 'snd_ui_click'],
    ['back', 'Назад, отмена', 'snd_ui_back'],
    ['start', 'Начало партии', 'snd_ui_start'],
    ['toggle_on', 'Флажок включён', 'snd_ui_toggle_on'],
    ['toggle_off', 'Флажок выключен', 'snd_ui_toggle_off'],
    ['select', 'Выбор в списке', 'snd_ui_select'],
    ['tab', 'Смена вкладки', 'snd_ui_tab'],
    ['tick', 'Шаг ползунка', 'snd_ui_tick'],
  ]),
  list('Музыка', [
    ['menu', 'Тема меню', 'mus_menu'],
    ['victory', 'Победа', 'mus_victory'],
    ['defeat', 'Поражение', 'mus_defeat'],
  ]),
];

const categories = [
  {id: 'units', title: 'Юниты', sections: unitSections},
  {id: 'buildings', title: 'Здания', sections: buildingSections},
  {id: 'events', title: 'События', sections: eventSections},
  {id: 'world', title: 'Мир', sections: worldSections},
  {id: 'menu', title: 'Меню', sections: menuSections},
];

// Файлы каждого звука — один раз; у места под звук только имя
const usedBy = {};
for (const c of categories)
  for (const s of c.sections) for (const card of s.cards) for (const sl of card.slots) if (sl.sound) usedBy[sl.sound] = (usedBy[sl.sound] ?? 0) + 1;
const library = {};
for (const sound of Object.keys(usedBy).sort(byNumber)) {
  library[sound] = {defined: sound in sounds, files: filesOf(sound), usedBy: usedBy[sound]};
}

const manifest = {built: new Date().toISOString(), categories, sounds: library};
fs.writeFileSync(path.join(here, 'manifest.js'), `window.SOUND_MANIFEST = ${JSON.stringify(manifest, null, 1)};\n`);

const withFiles = Object.values(library).filter((s) => s.files.length).length;
for (const c of categories) console.log(`${c.title}: ${c.sections.reduce((n, s) => n + s.cards.length, 0)} карточек`);
console.log(`Звуков: ${Object.keys(library).length}, с файлами ${withFiles}, без — ${Object.keys(library).length - withFiles}`);
console.log(`→ ${path.relative(root, path.join(here, 'manifest.js'))}`);
