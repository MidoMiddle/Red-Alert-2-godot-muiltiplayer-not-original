// Дубли звуков и голосов из ElevenLabs для страницы прослушки (tools/audio-picker). Запуск из корня репозитория:
//   node assets_src/audio/elevenlabs.mjs assets_src/audio/elevenlabs/<задание>.json   — сгенерировать дубли задания
//   node assets_src/audio/elevenlabs.mjs --voices                                     — голоса аккаунта: имя, id, метки
//
// Ключ берётся из связки ключей macOS (запись elevenlabs-api-key, как у MCP ElevenLabs проекта Video-creation) и никуда
// не пишется. Задание — список:
//   {"sound": "snd_rifle", "kind": "sfx", "prompt": "...", "duration": 1.0, "loop": false, "takes": 3}
//   {"sound": "vo_alliance_rifleman_select", "kind": "voice", "voice": "<voice_id>", "lines": ["...", "..."]}
// Звук: takes дублей по одному промпту. Голос: по дублю на каждую реплику. Файлы — assets_src/audio/takes/<звук>/<звук>_N.mp3,
// N продолжает уже лежащие; в takes.json рядом — промпт или реплика, длительность, голос, модель, формат, дата и тариф.
import {execFileSync} from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const takesRoot = path.join(here, 'takes');
const API = 'https://api.elevenlabs.io/v1';
const FORMAT = 'mp3_44100_192'; // тариф Creator и выше
const SFX_MODEL = 'eleven_text_to_sound_v2';
const VOICE_MODEL = 'eleven_multilingual_v2';

const key = execFileSync('security', ['find-generic-password', '-s', 'elevenlabs-api-key', '-w'], {encoding: 'utf8'}).trim();
const headers = {'xi-api-key': key, 'Content-Type': 'application/json'};

async function call(url, body) {
  const response = await fetch(url, body ? {method: 'POST', headers, body: JSON.stringify(body)} : {headers});
  if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);
  return response;
}

if (process.argv[2] === '--voices') {
  const {voices} = await (await call(`${API}/voices`)).json();
  for (const v of voices) console.log(`${v.name} · ${v.voice_id} · ${Object.values(v.labels ?? {}).join(', ')}`);
  process.exit(0);
}

const jobsFile = process.argv[2];
if (!jobsFile) throw new Error('Укажите задание: node assets_src/audio/elevenlabs.mjs assets_src/audio/elevenlabs/<задание>.json');
const jobs = JSON.parse(fs.readFileSync(jobsFile, 'utf8'));
const tier = (await (await call(`${API}/user/subscription`)).json()).tier ?? null;
const date = new Date().toISOString().slice(0, 10);

for (const job of jobs) {
  const dir = path.join(takesRoot, job.sound);
  fs.mkdirSync(dir, {recursive: true});
  const logFile = path.join(dir, 'takes.json');
  const log = fs.existsSync(logFile) ? JSON.parse(fs.readFileSync(logFile, 'utf8')) : [];
  const taken = fs.readdirSync(dir).map((n) => Number(n.match(new RegExp(`^${job.sound}_(\\d+)\\.mp3$`))?.[1] ?? 0));
  let next = Math.max(0, ...taken) + 1;

  const requests =
    job.kind === 'voice'
      ? job.lines.map((line) => ({
          url: `${API}/text-to-speech/${job.voice}?output_format=${FORMAT}`,
          body: {text: line, model_id: VOICE_MODEL, voice_settings: job.settings ?? {stability: 0.45, similarity_boost: 0.8, style: 0.35}},
          entry: {prompt: line, voice: job.voice, model: VOICE_MODEL},
        }))
      : Array.from({length: job.takes ?? 3}, () => ({
          url: `${API}/sound-generation?output_format=${FORMAT}`,
          body: {text: job.prompt, model_id: SFX_MODEL, duration_seconds: job.duration ?? null, loop: !!job.loop, prompt_influence: job.influence ?? 0.4},
          entry: {prompt: job.prompt, duration: job.duration ?? null, loop: !!job.loop, model: SFX_MODEL},
        }));

  for (const request of requests) {
    const file = `${job.sound}_${next++}.mp3`;
    const audio = Buffer.from(await (await call(request.url, request.body)).arrayBuffer());
    fs.writeFileSync(path.join(dir, file), audio);
    log.push({file, ...request.entry, seed: null, date, tier, format: FORMAT});
    fs.writeFileSync(logFile, JSON.stringify(log, null, 2) + '\n');
    console.log(`${path.relative(process.cwd(), path.join(dir, file))} · ${(audio.length / 1024).toFixed(0)} КБ`);
  }
}
