// Sleeve — interface do player (Windows e Alba OS).
// Toca os arquivos da sua biblioteca e mostra/controla o Spotify.
import { host } from './bridge.js';
import { t as tx, setLang, resolveLang, NAMES, lang } from './i18n.js';

// ---------- Utilidades ----------
const $ = (s, el = document) => el.querySelector(s);
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const fmt = (sec) => {
  if (!Number.isFinite(sec) || sec < 0) sec = 0;
  const m = Math.floor(sec / 60), s = Math.floor(sec % 60), h = Math.floor(m / 60);
  return h ? `${h}:${String(m % 60).padStart(2, '0')}:${String(s).padStart(2, '0')}` : `${m}:${String(s).padStart(2, '0')}`;
};
const norm = (s) => String(s ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
const load = (k, d) => { try { const v = localStorage.getItem('sleeve.' + k); return v == null ? d : JSON.parse(v); } catch { return d; } };
const save = (k, v) => { try { localStorage.setItem('sleeve.' + k, JSON.stringify(v)); } catch { /* sem armazenamento */ } };

const svg = (body, cls = '') => `<svg viewBox="0 0 24 24" aria-hidden="true"${cls ? ` class="${cls}"` : ''}>${body}</svg>`;
const I = {
  play: svg('<path class="fill" d="M7 4.5v15l12-7.5z"/>'),
  pause: svg('<path d="M8 5v14M16 5v14"/>'),
  prev: svg('<path d="M19 20L9 12l10-8z"/><path d="M5 19V5"/>'),
  next: svg('<path d="M5 4l10 8-10 8z"/><path d="M19 5v14"/>'),
  shuffle: svg('<path d="M16 3h5v5"/><path d="M4 20L21 3"/><path d="M21 16v5h-5"/><path d="M15 15l6 6"/><path d="M4 4l5 5"/>'),
  repeat: svg('<path d="M17 2l4 4-4 4"/><path d="M3 11V9a3 3 0 0 1 3-3h15"/><path d="M7 22l-4-4 4-4"/><path d="M21 13v2a3 3 0 0 1-3 3H3"/>'),
  repeatOne: svg('<path d="M17 2l4 4-4 4"/><path d="M3 11V9a3 3 0 0 1 3-3h15"/><path d="M7 22l-4-4 4-4"/><path d="M21 13v2a3 3 0 0 1-3 3H3"/><path d="M11 10h1v5"/>'),
  heart: svg('<path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21.2l8.8-8.8a5.5 5.5 0 0 0 0-7.8z"/>'),
  heartOn: svg('<path class="fill" d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21.2l8.8-8.8a5.5 5.5 0 0 0 0-7.8z"/>'),
  back: svg('<path d="M15 18l-6-6 6-6"/>'),
  down: svg('<path d="M6 9l6 6 6-6"/>'),
  volume: svg('<path d="M11 5L6 9H2v6h4l5 4z"/><path d="M15.5 8.5a5 5 0 0 1 0 7"/>'),
  mute: svg('<path d="M11 5L6 9H2v6h4l5 4z"/><path d="M22 9l-6 6M16 9l6 6"/>'),
  expand: svg('<path d="M15 3h6v6"/><path d="M9 21H3v-6"/><path d="M21 3l-7 7"/><path d="M3 21l7-7"/>'),
  albums: svg('<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>'),
  artists: svg('<circle cx="12" cy="8" r="4"/><path d="M4 21c1.5-4 4.5-6 8-6s6.5 2 8 6"/>'),
  music: svg('<path d="M9 18V5l11-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="17" cy="16" r="3"/>'),
  folder: svg('<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  plus: svg('<path d="M12 5v14M5 12h14"/>'),
  refresh: svg('<path d="M21 12a9 9 0 1 1-3-6.7L21 8"/><path d="M21 3v5h-5"/>'),
  trash: svg('<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>'),
  mini: svg('<rect x="3" y="14" width="10" height="7" rx="1.5"/><path d="M21 10V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v5"/>'),
  minimize: svg('<path d="M5 12h14"/>'),
  close: svg('<path d="M6 6l12 12M18 6L6 18"/>'),
  lyrics: svg('<path d="M12 3a3 3 0 0 0-3 3v6a3 3 0 0 0 6 0V6a3 3 0 0 0-3-3z"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/>'),
  queueAdd: svg('<path d="M4 6h12M4 11h12M4 16h7"/><path d="M18 14v6M15 17h6"/>'),
  playNext: svg('<path d="M4 6h10M4 11h10M4 16h6"/><path class="fill" d="M15 13v8l6-4z"/>'),
  album: svg('<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="2.5"/>'),
  pin: svg('<path d="M9 4h6l-1 5 4 4H6l4-4z"/><path d="M12 13v8"/>'),
};
const MARK = '<svg class="mark" viewBox="0 0 120 120" aria-hidden="true"><line x1="30.2" y1="58.6" x2="74.2" y2="25.6"/><line x1="45.8" y1="94.4" x2="89.8" y2="61.4"/></svg>';
const coverHtml = (src, alt = '') => `<div class="cover">${src ? `<img src="${esc(src)}" alt="${esc(alt)}" loading="lazy" decoding="async">` : MARK}</div>`;

// ---------- Cores tiradas da capa ----------
function hexToHsv(hex) {
  const n = parseInt(String(hex || '#7C3AED').replace('#', ''), 16) || 0x7c3aed;
  const r = (n >> 16 & 255) / 255, g = (n >> 8 & 255) / 255, b = (n & 255) / 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
  let h = 0;
  if (d) h = max === r ? 60 * (((g - b) / d) % 6) : max === g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
  if (h < 0) h += 360;
  return [h, max ? d / max : 0, max];
}
function hsv(h, s, v, a = 1) {
  const c = v * s, x = c * (1 - Math.abs((h / 60) % 2 - 1)), m = v - c;
  const [r, g, b] = h < 60 ? [c, x, 0] : h < 120 ? [x, c, 0] : h < 180 ? [0, c, x] : h < 240 ? [0, x, c] : h < 300 ? [x, 0, c] : [c, 0, x];
  const f = (k) => Math.round((k + m) * 255);
  return a < 1 ? `rgba(${f(r)}, ${f(g)}, ${f(b)}, ${a})` : `rgb(${f(r)}, ${f(g)}, ${f(b)})`;
}
let lastColor = null;
function applyColors(hex) {
  const key = hex || '#7C3AED';
  if (key === lastColor) return;
  lastColor = key;
  const [h, s] = hexToHsv(key);
  const st = document.documentElement.style;
  st.setProperty('--np-base', hsv(h, Math.min(s, .75), .16));
  st.setProperty('--np-tint', hsv(h, Math.min(s, .75), .2, .66));
  st.setProperty('--np-fg2', hsv(h, Math.min(s, .3), .9));
  st.setProperty('--np-track', hsv(h, Math.min(s, .3), .9, .3));
  st.setProperty('--np-play', hsv(h, Math.min(s, .12), .98));
  st.setProperty('--np-glyph', hsv(h, Math.min(s, .7), .18));
}

// ---------- Estado ----------
const audio = $('#audio');
const S = {
  albums: [], albumById: new Map(), trackById: new Map(), allTracks: [],
  folders: [], scanning: false, scanCount: 0,
  settings: { hiRes: true, startup: false, pinned: false, mini: false, autoUpdate: true },
  update: { state: 'idle', version: null, current: '' },
  view: { name: 'albums' }, history: [],
  sort: load('sort', 'recent'),
  search: '',
  spotify: null, spotifyAt: 0,
  queue: [], baseQueue: [], index: -1,
  shuffle: load('shuffle', false), repeat: load('repeat', 'off'),
  likes: new Set(load('likes', [])), plays: load('plays', {}),
  volume: load('volume', 0.85), muted: false,
  nowOpen: false,
  lyricsOn: load('lyricsOn', false), onlineLyrics: load('onlineLyrics', true),
  lyr: { key: '', state: 'idle', lines: null, plain: '' }, // letra da música atual
  lang: load('lang', 'auto'), // 'auto' segue o idioma do sistema
  theme: load('theme', 'auto'), // 'auto' segue o Windows/Alba; 'light' ou 'dark' fixos
  lists: {},  // listas de faixas visíveis, para tocar a partir de um clique
};
audio.volume = S.volume;

const current = () => S.queue[S.index] ?? null;

// ---------- Tema claro / escuro ----------
const darkQuery = window.matchMedia('(prefers-color-scheme: dark)');
function applyTheme() {
  const dark = S.theme === 'dark' || (S.theme === 'auto' && darkQuery.matches);
  document.documentElement.dataset.theme = dark ? 'dark' : 'light';
  host.send({ type: 'theme', dark }); // a janela do sistema acompanha a cor de fundo
}
darkQuery.addEventListener('change', () => { if (S.theme === 'auto') applyTheme(); });

// Idioma: textos fixos do index.html usam data-i18n e data-i18n-attr="atributo:chave,…"
function applyLang() {
  setLang(resolveLang(S.lang));
  document.querySelectorAll('[data-i18n]').forEach((el) => { el.textContent = tx(el.dataset.i18n); });
  document.querySelectorAll('[data-i18n-attr]').forEach((el) => {
    for (const pair of el.dataset.i18nAttr.split(',')) { const [attr, key] = pair.split(':'); el.setAttribute(attr, tx(key)); }
  });
  renderSpotifyState();
  host.send({ type: 'language', lang }); // bandeja e janelas do sistema acompanham
}
function renderSpotifyState() {
  const el = document.getElementById('spotify-state');
  if (el) el.textContent = S.spotify ? tx(S.spotify.playing ? 'sp.playing' : 'sp.connected') : tx('sp.closed');
}
let dragging = null; // barra sendo arrastada agora

// Quem aparece no "tocando agora": o Sleeve (arquivos) ou o Spotify
function mode() {
  const t = current();
  if (t && !audio.paused) return 'local';
  if (S.spotify?.playing) return 'spotify';
  if (t) return 'local';
  if (S.spotify) return 'spotify';
  return null;
}

function spotifyPosition() {
  const sp = S.spotify;
  if (!sp) return 0;
  const pos = sp.position + (sp.playing ? (performance.now() - S.spotifyAt) / 1000 : 0);
  return Math.min(Math.max(pos, 0), sp.duration || pos);
}

function info() {
  const m = mode();
  if (m === 'local') {
    const t = current(), a = S.albumById.get(t.albumId);
    return {
      mode: m, title: t.title, artist: t.artist || a?.artist || '', album: a?.title || '', cover: a?.cover, color: a?.color,
      playing: !audio.paused, position: audio.currentTime || 0, duration: audio.duration || t.duration || 0,
      canSeek: true, canPrevious: true, canNext: true, source: tx('src.files'), liked: S.likes.has(t.id),
    };
  }
  if (m === 'spotify') {
    const sp = S.spotify;
    return {
      mode: m, title: sp.title, artist: sp.artist, album: sp.album, cover: sp.cover, color: sp.color,
      playing: sp.playing, position: spotifyPosition(), duration: sp.duration || 0,
      canSeek: sp.canSeek, canPrevious: sp.canPrevious, canNext: sp.canNext, source: sp.source || 'Spotify', liked: null,
    };
  }
  return null;
}

// ---------- Biblioteca ----------
function setLibrary(msg) {
  S.albums = msg.albums || [];
  S.folders = msg.folders || [];
  S.scanning = !!msg.scanning;
  if (!S.scanning) S.scanCount = 0;
  // Músicas abertas fora da biblioteca continuam com o "álbum" delas
  const loose = [...S.albumById.values()].filter((a) => a.loose);
  S.albumById = new Map([...S.albums, ...loose].map((a) => [a.id, a]));
  S.trackById = new Map();
  S.allTracks = [];
  for (const a of S.albums) {
    for (const t of a.tracks) {
      t.albumId = a.id;
      S.trackById.set(t.id, t);
      S.allTracks.push(t);
    }
  }
  // A fila continua valendo se as faixas ainda existirem
  const keep = (t) => (t.loose ? t : S.trackById.get(t.id));
  S.queue = S.queue.map(keep).filter(Boolean);
  S.baseQueue = S.baseQueue.map(keep).filter(Boolean);
  if (S.index >= S.queue.length) S.index = S.queue.length - 1;
  renderAll();
}

const artistOf = (a) => a.artist || tx('lib.unknownArtist');
function artists() {
  const map = new Map();
  for (const a of S.albums) {
    const k = artistOf(a);
    if (!map.has(k)) map.set(k, []);
    map.get(k).push(a);
  }
  return [...map.entries()].sort((x, y) => norm(x[0]).localeCompare(norm(y[0]))).map(([name, albums]) => ({ name, albums }));
}
function albumPlays(a) { return a.tracks.reduce((n, t) => n + (S.plays[t.id] || 0), 0); }
function sortedAlbums(list = S.albums) {
  const l = [...list];
  if (S.sort === 'az') l.sort((x, y) => norm(x.title).localeCompare(norm(y.title)));
  else if (S.sort === 'artist') l.sort((x, y) => norm(artistOf(x)).localeCompare(norm(artistOf(y))) || (x.year || 0) - (y.year || 0));
  else if (S.sort === 'plays') l.sort((x, y) => albumPlays(y) - albumPlays(x));
  else l.sort((x, y) => (y.added || 0) - (x.added || 0));
  return l;
}

// ---------- Músicas abertas pelo sistema (duplo clique num arquivo) ----------
function openFiles(list) {
  const tracks = list.map((o) => {
    const inLibrary = S.trackById.get(o.id);
    if (inLibrary) return inLibrary;
    const albumId = 'avulso-' + o.id;
    S.albumById.set(albumId, { id: albumId, title: o.album || '', artist: o.artist || '', cover: o.cover, color: o.color, tracks: [], loose: true });
    return { id: o.id, title: o.title, artist: o.artist, number: o.number || 1, disc: 1, duration: o.duration, url: o.url, albumId, loose: true };
  });
  if (!tracks.length) return;
  playList(tracks, 0);
  openNow(true);
}

// ---------- Tocar ----------
function shuffleArray(arr) {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}
function playList(tracks, start = 0, forceShuffle = false) {
  if (!tracks.length) return;
  if (forceShuffle) { S.shuffle = true; save('shuffle', true); }
  S.baseQueue = [...tracks];
  if (S.shuffle) {
    const first = forceShuffle ? tracks[Math.floor(Math.random() * tracks.length)] : tracks[start];
    S.queue = [first, ...shuffleArray(tracks.filter((t) => t !== first))];
    S.index = 0;
  } else {
    S.queue = [...tracks];
    S.index = start;
  }
  loadCurrent(true);
}
function loadCurrent(autoplay) {
  const t = current();
  if (!t) return;
  if (t.url) {
    audio.src = t.url;
    if (autoplay) audio.play().catch(() => {});
  }
  S.plays[t.id] = (S.plays[t.id] || 0) + 1;
  save('plays', S.plays);
  renderPlayback(true);
}
function next(auto = false) {
  if (!S.queue.length) return;
  if (auto && S.repeat === 'one') { audio.currentTime = 0; audio.play().catch(() => {}); return; }
  if (S.index < S.queue.length - 1) S.index++;
  else if (S.repeat === 'all' || !auto) S.index = 0;
  else { audio.pause(); audio.currentTime = 0; renderPlayback(); return; }
  loadCurrent(true);
}
function previous() {
  if (audio.currentTime > 3 || S.index <= 0) { audio.currentTime = 0; if (audio.paused) audio.play().catch(() => {}); return; }
  S.index--;
  loadCurrent(true);
}
function toggleShuffle() {
  S.shuffle = !S.shuffle;
  save('shuffle', S.shuffle);
  const t = current();
  if (t) {
    if (S.shuffle) { S.queue = [t, ...shuffleArray(S.baseQueue.filter((x) => x !== t))]; S.index = 0; }
    else { S.queue = [...S.baseQueue]; S.index = Math.max(0, S.queue.indexOf(t)); }
  }
  renderPlayback();
}
function cycleRepeat() {
  S.repeat = S.repeat === 'off' ? 'all' : S.repeat === 'all' ? 'one' : 'off';
  save('repeat', S.repeat);
  renderPlayback();
}

// ---------- Fila: tocar a seguir, adicionar, tirar e reordenar ----------
function enqueue(tracks, nextUp) {
  tracks = tracks.filter(Boolean);
  if (!tracks.length) return;
  if (!current()) { playList(tracks, 0); return; }
  if (nextUp) {
    S.queue.splice(S.index + 1, 0, ...tracks);
    const b = S.baseQueue.indexOf(current());
    S.baseQueue.splice(b < 0 ? S.baseQueue.length : b + 1, 0, ...tracks);
  } else {
    S.queue.push(...tracks);
    S.baseQueue.push(...tracks);
  }
  renderPlayback();
  toast(tx(nextUp ? 'queue.toastNext' : 'queue.toastAdd', { t: tracks.length === 1 ? tracks[0].title : tx('album.tracksN', { n: tracks.length }) }));
}
function dequeue(i) {
  if (i <= S.index || i >= S.queue.length) return;
  const [t] = S.queue.splice(i, 1);
  // Tira da lista base só se a faixa não aparecer mais na fila
  if (!S.queue.includes(t)) { const b = S.baseQueue.indexOf(t); if (b >= 0) S.baseQueue.splice(b, 1); }
  renderPlayback();
}
function moveInQueue(from, to) {
  if (from <= S.index || to <= S.index || from === to || from >= S.queue.length) return;
  const [t] = S.queue.splice(from, 1);
  S.queue.splice(Math.min(to, S.queue.length), 0, t);
  renderPlayback();
}

let toastTimer = 0;
function toast(text) {
  let el = document.getElementById('toast');
  if (!el) { el = document.createElement('div'); el.id = 'toast'; el.className = 'toast'; el.setAttribute('role', 'status'); document.body.append(el); }
  el.textContent = text;
  el.classList.add('on');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove('on'), 2200);
}

// ---------- Letras ----------
const lyricCache = new Map();
const lyricsKey = (i) => (i ? `${i.title}\u0000${i.artist}` : '');
/** Lê uma letra no formato LRC ([mm:ss.xx] texto) e devolve as linhas em ordem de tempo. */
function parseLrc(text) {
  const out = [];
  for (const raw of String(text).split(/\r?\n/)) {
    const tags = [...raw.matchAll(/\[(\d+):(\d+(?:[.:]\d+)?)\]/g)];
    if (!tags.length) continue;
    const words = raw.replace(/\[[^\]]*\]/g, '').replace(/<[^>]*>/g, '').trim();
    for (const m of tags) out.push({ t: +m[1] * 60 + parseFloat(m[2].replace(':', '.')), text: words });
  }
  out.sort((a, b) => a.t - b.t);
  // Linhas vazias no começo não servem para nada
  while (out.length && !out[0].text) out.shift();
  return out;
}
/** Pede a letra da música atual quando o painel de letra está aberto. */
function wantLyrics(i) {
  if (!S.lyricsOn || !S.nowOpen || !i) return;
  const key = lyricsKey(i);
  if (S.lyr.key === key) return;
  if (lyricCache.has(key)) { S.lyr = { key, ...lyricCache.get(key) }; return; }
  S.lyr = { key, state: 'loading', lines: null, plain: '' };
  const t = i.mode === 'local' ? current() : null;
  host.send({
    type: 'lyrics', key, title: i.title, artist: i.artist, album: i.album,
    duration: Math.round(i.duration || 0), url: t?.url || null, online: !!S.onlineLyrics,
  });
}
function gotLyrics(msg) {
  const lines = msg.synced ? parseLrc(msg.synced) : [];
  const entry = { state: lines.length || msg.plain ? 'ok' : 'none', lines: lines.length ? lines : null, plain: lines.length ? '' : (msg.plain || '').trim() };
  if (msg.key) lyricCache.set(msg.key, entry);
  if (msg.key === S.lyr.key) { S.lyr = { key: msg.key, ...entry }; renderPlayback(); }
}
function lyricsHtml() {
  const L = S.lyr;
  const tag = `data-key="${esc(L.key + '|' + L.state)}"`;
  if (L.lines) {
    return `<div class="lyr-scroll" ${tag} data-k="-2">${L.lines.map((l, k) =>
      `<p class="lyr-line${l.text ? '' : ' gap'}" data-act="lyr-seek" data-t="${l.t}">${l.text ? esc(l.text) : '♪'}</p>`).join('')}</div>`;
  }
  if (L.plain) return `<div class="lyr-scroll plain" ${tag}>${esc(L.plain).replace(/\n/g, '<br>')}</div>`;
  return `<div class="lyr-scroll" ${tag}><p class="lyr-note">${tx(L.state === 'loading' ? 'lyrics.loading' : 'lyrics.none')}</p></div>`;
}
let lyrUserUntil = 0; // rolou a letra com a mão: espera um pouco antes de voltar a acompanhar
function paintLyrics(pos, instant = false) {
  const box = document.querySelector('.lyr-scroll[data-k]');
  if (!box || !S.lyr.lines) return;
  const lines = S.lyr.lines;
  let k = -1;
  for (let j = 0; j < lines.length; j++) { if (lines[j].t <= pos + 0.25) k = j; else break; }
  if (!instant && String(k) === box.dataset.k) return;
  box.dataset.k = k;
  const els = box.children;
  for (let j = 0; j < els.length; j++) { els[j].classList.toggle('on', j === k); els[j].classList.toggle('past', j < k); }
  if (performance.now() < lyrUserUntil) return;
  const el = els[Math.max(0, k)];
  if (el) box.scrollTo({ top: el.offsetTop - box.clientHeight * 0.38 + el.offsetHeight / 2, behavior: instant ? 'auto' : 'smooth' });
}

// ---------- Menu do botão direito ----------
let menuAt = 0;
function closeMenu() { document.getElementById('ctx')?.remove(); }
function openMenu(x, y, items) {
  closeMenu();
  menuAt = performance.now();
  const el = document.createElement('div');
  el.id = 'ctx';
  el.className = 'ctx';
  el.setAttribute('role', 'menu');
  el.innerHTML = items.map((it, k) => `<button type="button" role="menuitem" data-k="${k}">${it.icon || ''}${esc(it.label)}</button>`).join('');
  document.body.append(el);
  const r = el.getBoundingClientRect();
  el.style.left = Math.min(x, innerWidth - r.width - 8) + 'px';
  el.style.top = Math.min(y, innerHeight - r.height - 8) + 'px';
  el.addEventListener('click', (e) => {
    const b = e.target.closest('[data-k]');
    if (!b) return;
    e.stopPropagation();
    closeMenu();
    items[+b.dataset.k].run();
  });
  el.querySelector('button')?.focus();
}
document.addEventListener('contextmenu', (e) => {
  const li = e.target.closest('[data-act="play"]');
  const q = e.target.closest('.queue [data-act="jump"]');
  if (!li && !q) return;
  e.preventDefault();
  if (q) {
    const i = +q.dataset.i;
    openMenu(e.clientX, e.clientY, [
      { label: tx('common.play'), icon: I.play, run: () => { S.index = i; loadCurrent(true); } },
      { label: tx('queue.remove'), icon: I.close, run: () => dequeue(i) },
    ]);
    return;
  }
  const list = S.lists[li.dataset.list] || [];
  const t = list[+li.dataset.i];
  if (!t) return;
  const a = S.albumById.get(t.albumId);
  const items = [
    { label: tx('common.play'), icon: I.play, run: () => playList(list, +li.dataset.i) },
    { label: tx('queue.playNext'), icon: I.playNext, run: () => enqueue([t], true) },
    { label: tx('queue.add'), icon: I.queueAdd, run: () => enqueue([t], false) },
  ];
  if (a && !a.loose && S.view.name !== 'album') items.push({ label: tx('menu.goAlbum'), icon: I.album, run: () => go({ name: 'album', id: a.id }) });
  if (a && !a.loose && S.view.name !== 'artist') items.push({ label: tx('menu.goArtist'), icon: I.artists, run: () => go({ name: 'artist', id: artistOf(a) }) });
  openMenu(e.clientX, e.clientY, items);
});
document.addEventListener('pointerdown', (e) => { if (!e.target.closest('#ctx')) closeMenu(); }, true);
window.addEventListener('blur', closeMenu);
// Rolar a tela fecha o menu (menos a rolagem que já estava acontecendo quando ele abriu)
document.addEventListener('scroll', () => { if (performance.now() - menuAt > 250) closeMenu(); }, true);

// Arrastar faixas no "A seguir" para mudar a ordem
let dragFrom = -1;
document.addEventListener('dragstart', (e) => {
  const q = e.target.closest?.('.queue [data-act="jump"]');
  if (!q) return;
  dragFrom = +q.dataset.i;
  e.dataTransfer.effectAllowed = 'move';
  e.dataTransfer.setData('text/plain', String(dragFrom));
  q.classList.add('dragging');
});
document.addEventListener('dragover', (e) => {
  const q = e.target.closest?.('.queue [data-act="jump"]');
  if (dragFrom < 0 || !q) return;
  e.preventDefault();
  document.querySelectorAll('.queue .q.over').forEach((x) => x !== q && x.classList.remove('over', 'after'));
  q.classList.add('over');
  q.classList.toggle('after', dragFrom < +q.dataset.i); // descendo: entra depois; subindo: entra antes
});
document.addEventListener('drop', (e) => {
  const q = e.target.closest?.('.queue [data-act="jump"]');
  if (dragFrom < 0 || !q) return;
  e.preventDefault();
  const from = dragFrom;
  dragFrom = -1;
  moveInQueue(from, +q.dataset.i);
});
document.addEventListener('dragend', () => {
  dragFrom = -1;
  document.querySelectorAll('.queue .q.over, .queue .q.dragging').forEach((x) => x.classList.remove('over', 'after', 'dragging'));
});

// Comandos que valem para quem estiver tocando
const cmd = {
  toggle() {
    const m = mode();
    if (m === 'local') { if (audio.paused) audio.play().catch(() => {}); else audio.pause(); }
    else if (m === 'spotify') host.send({ type: 'spotify', action: 'toggle' });
  },
  next() { mode() === 'spotify' ? host.send({ type: 'spotify', action: 'next' }) : next(); },
  previous() { mode() === 'spotify' ? host.send({ type: 'spotify', action: 'previous' }) : previous(); },
  seek(ratio) {
    const i = info();
    if (!i || !i.canSeek || !i.duration) return;
    const pos = ratio * i.duration;
    if (i.mode === 'local') audio.currentTime = pos;
    else { S.spotify.position = pos; S.spotifyAt = performance.now(); host.send({ type: 'spotify', action: 'seek', position: pos }); }
    tick(true);
  },
  volume(v) {
    v = Math.min(1, Math.max(0, v));
    if (mode() === 'spotify') { S.spotify.volume = v; host.send({ type: 'volume', app: S.spotify.source || 'Spotify', value: v }); }
    else { S.volume = v; audio.volume = v; audio.muted = false; save('volume', v); }
    renderVolume();
  },
  like() {
    const t = current();
    if (!t || mode() !== 'local') return;
    if (S.likes.has(t.id)) S.likes.delete(t.id); else S.likes.add(t.id);
    save('likes', [...S.likes]);
    renderPlayback();
    if (S.view.name === 'likes') renderView();
  },
};

audio.addEventListener('play', () => {
  // Começou a tocar um arquivo: o Spotify para, para não tocarem os dois juntos
  if (S.spotify?.playing) host.send({ type: 'spotify', action: 'pause' });
  reportLocal();
  renderPlayback();
});
audio.addEventListener('pause', () => { reportLocal(); renderPlayback(); });
audio.addEventListener('ended', () => next(true));
audio.addEventListener('loadedmetadata', () => tick(true));
audio.addEventListener('error', () => {
  // Arquivo que não abre (apagado, formato que o sistema não toca): pula para a próxima
  if (audio.src && S.queue.length > 1) setTimeout(() => next(true), 400);
});
function reportLocal() {
  const t = current();
  host.send({ type: 'local', title: t?.title || '', artist: t?.artist || '', playing: !!t && !audio.paused });
}

// ---------- Desenho: navegação e telas ----------
const NAV = [
  ['albums', 'nav.albums', I.albums],
  ['artists', 'nav.artists', I.artists],
  ['tracks', 'nav.tracks', I.music],
  ['likes', 'nav.likes', I.heart],
  ['folders', 'nav.folders', I.folder],
];
function renderNav() {
  const active = S.view.name === 'album' ? 'albums' : S.view.name === 'artist' ? 'artists' : S.view.name;
  $('#nav').innerHTML = NAV.map(([k, label, icon]) =>
    `<button type="button" class="nav-item${active === k ? ' active' : ''}" data-view="${k}">${icon}${tx(label)}</button>`).join('');
  document.querySelector('.nav-item.settings').classList.toggle('active', active === 'ajustes');
  $('#files-count').textContent = S.scanning ? (S.scanCount ? tx('src.readingN', { n: S.scanCount }) : tx('src.reading')) : tx('src.tracksN', { n: S.allTracks.length });
}

function go(view, push = true) {
  if (push && S.view.name !== view.name || push && view.id !== S.view.id) S.history.push(S.view);
  S.view = view;
  if (view.name !== 'search') { S.search = ''; $('#search').value = ''; }
  renderNav();
  renderView();
  $('#view').scrollTop = 0;
}
function goBack() {
  const prev = S.history.pop() || { name: 'albums' };
  go(prev, false);
}

function albumGrid(list) {
  return `<div class="grid">${list.map((a) => `
    <div class="card">
      <button type="button" class="card-open" data-act="album" data-id="${esc(a.id)}" style="all:unset;cursor:pointer;display:flex;flex-direction:column;gap:10px;min-width:0">
        ${coverHtml(a.cover, a.title)}
        <span class="t1">${esc(a.title)}</span>
        <span class="t2">${esc(artistOf(a))}</span>
      </button>
    </div>`).join('')}</div>`;
}

function trackList(key, tracks, { compact = false, showAlbum = true } = {}) {
  S.lists[key] = tracks;
  const cur = mode() === 'local' ? current() : null;
  return `<ol class="tracks">${tracks.map((t, i) => {
    const a = S.albumById.get(t.albumId);
    const playing = cur && cur.id === t.id;
    return `<li tabindex="0" class="track${compact ? ' compact' : ''}${playing ? ' playing' : ''}" data-act="play" data-list="${esc(key)}" data-i="${i}" title="${tx('track.hint')}">
      <span class="n">${compact ? (t.number || i + 1) : i + 1}</span>
      <span class="title">${esc(t.title)}</span>
      ${compact ? '' : `<span class="sub">${esc(t.artist || a?.artist || '')}${showAlbum && a ? ' · ' + esc(a.title) : ''}</span>`}
      <span class="dur">${fmt(t.duration)}</span>
    </li>`;
  }).join('')}</ol>`;
}

function emptyLibrary() {
  return `<div class="empty">${MARK}
    <h1>${S.scanning ? tx('lib.reading') : tx('lib.empty')}</h1>
    <p>${S.scanning ? tx('lib.foundSoFar', { n: S.scanCount }) : tx('lib.emptyHint')}</p>
    ${S.scanning ? '' : `<button type="button" class="btn primary" data-act="add-folder">${I.plus}${tx('lib.addFolder')}</button>`}
  </div>`;
}

const SORTS = [['recent', 'sort.recent'], ['az', 'sort.az'], ['artist', 'sort.artist'], ['plays', 'sort.plays']];

function renderView() {
  const v = S.view, el = $('#view');
  S.lists = {};
  let html = '';
  if (v.name === 'albums') {
    html = `<div class="view-head"><h1>${tx('lib.title')}</h1>
      <div class="chips">${SORTS.map(([k, l]) => `<button type="button" class="chip${S.sort === k ? ' on' : ''}" data-act="sort" data-sort="${k}">${tx(l)}</button>`).join('')}</div></div>`;
    html += S.albums.length ? albumGrid(sortedAlbums()) : emptyLibrary();
  } else if (v.name === 'album') {
    const a = S.albumById.get(v.id);
    if (!a) { go({ name: 'albums' }, false); return; }
    const total = a.tracks.reduce((n, t) => n + (t.duration || 0), 0);
    html = `<button type="button" class="back" data-act="back">${I.back}${tx('common.back')}</button>
      <div class="album-head">${coverHtml(a.cover, a.title)}
        <div class="album-info">
          <span class="eyebrow">${tx('album.eyebrow')}</span>
          <h1>${esc(a.title)}</h1>
          <button type="button" class="artist" data-act="artist" data-name="${esc(artistOf(a))}" style="all:unset;cursor:pointer;font-size:17px;font-weight:600">${esc(artistOf(a))}</button>
          <span class="t2">${[a.year, tx(a.tracks.length === 1 ? 'album.track1' : 'album.tracksN', { n: a.tracks.length }), fmt(total)].filter(Boolean).join(' · ')}</span>
          <div class="actions">
            <button type="button" class="btn primary" data-act="play-album" data-id="${esc(a.id)}">${I.play}${tx('common.play')}</button>
            <button type="button" class="btn" data-act="shuffle-album" data-id="${esc(a.id)}">${I.shuffle}${tx('common.shuffle')}</button>
          </div>
        </div>
      </div>
      ${trackList('album', a.tracks, { compact: true })}`;
  } else if (v.name === 'artists') {
    const list = artists();
    html = `<div class="view-head"><h1>${tx('nav.artists')}</h1></div>` + (list.length ? `<div class="artists">${list.map((ar) => `
      <button type="button" class="artist-card" data-act="artist" data-name="${esc(ar.name)}">
        ${coverHtml(ar.albums.find((a) => a.cover)?.cover, ar.name)}
        <span class="t1" style="max-width:100%">${esc(ar.name)}</span>
        <span class="t2">${tx(ar.albums.length === 1 ? 'album.album1' : 'album.albumsN', { n: ar.albums.length })}</span>
      </button>`).join('')}</div>` : emptyLibrary());
  } else if (v.name === 'artist') {
    const list = sortedAlbums(S.albums.filter((a) => artistOf(a) === v.id));
    const tracks = list.flatMap((a) => a.tracks);
    html = `<button type="button" class="back" data-act="back">${I.back}${tx('common.back')}</button>
      <div class="view-head"><h1>${esc(v.id)}</h1>
        <div class="chips"><button type="button" class="btn primary" data-act="play-list" data-list="artist">${I.play}${tx('common.playAll')}</button></div></div>
      ${albumGrid(list)}
      <h2 class="section-title">${tx('nav.tracks')}</h2>${trackList('artist', tracks)}`;
  } else if (v.name === 'tracks') {
    const tracks = [...S.allTracks].sort((x, y) => norm(x.title).localeCompare(norm(y.title)));
    html = `<div class="view-head"><h1>${tx('nav.tracks')}</h1>
      <div class="chips"><button type="button" class="btn" data-act="shuffle-list" data-list="all">${I.shuffle}${tx('common.shuffle')}</button></div></div>`
      + (tracks.length ? trackList('all', tracks) : emptyLibrary());
  } else if (v.name === 'likes') {
    const tracks = S.allTracks.filter((t) => S.likes.has(t.id));
    html = `<div class="view-head"><h1>${tx('nav.likes')}</h1>${tracks.length ? `<div class="chips"><button type="button" class="btn primary" data-act="play-list" data-list="likes">${I.play}${tx('common.play')}</button></div>` : ''}</div>`
      + (tracks.length ? trackList('likes', tracks) : `<div class="empty">${MARK}<p>${tx('likes.empty')}</p></div>`);
  } else if (v.name === 'folders') {
    html = `<div class="view-head"><h1>${tx('nav.folders')}</h1></div>
      <div class="panel">
        ${S.folders.map((f) => `<div class="row">${I.folder.replace('<svg', '<svg style="width:20px;height:20px;flex:none;color:var(--brand-violet)"')}
          <span class="grow path" title="${esc(f)}">${esc(f)}</span>
          <button type="button" class="icon-btn" data-act="remove-folder" data-path="${esc(f)}" aria-label="${tx('folders.remove', { path: esc(f) })}">${I.trash}</button></div>`).join('')
        || `<div class="row"><span class="grow t2">${tx('folders.none')}</span></div>`}
      </div>
      <div class="actions">
        <button type="button" class="btn primary" data-act="add-folder">${I.plus}${tx('lib.addFolder')}</button>
        <button type="button" class="btn" data-act="rescan"${S.scanning ? ' disabled' : ''}>${I.refresh}${S.scanning ? tx('folders.readingN', { n: S.scanCount }) : tx('folders.rescan')}</button>
      </div>
      <p class="note">${tx('folders.note')}</p>`;
  } else if (v.name === 'ajustes') {
    const sw = (key, on, label, sub) => `<div class="row"><div class="grow"><div class="t1" style="white-space:normal">${label}</div>${sub ? `<div class="t2" style="white-space:normal;font-size:13px">${sub}</div>` : ''}</div>
      <button type="button" class="switch" role="switch" aria-checked="${!!on}" aria-label="${esc(label)}" data-act="setting" data-key="${key}"></button></div>`;
    const themeBtn = (k, label) => `<button type="button" data-act="theme" data-theme="${k}" aria-pressed="${S.theme === k}">${label}</button>`;
    const langBtn = (k, label) => `<button type="button" data-act="lang" data-lang="${k}" aria-pressed="${S.lang === k}">${label}</button>`;
    const segRow = (title, hint, group) => `<div class="row"><div class="grow"><div class="t1" style="white-space:normal">${title}</div>
          <div class="t2" style="white-space:normal;font-size:13px">${hint}</div></div>
          <div class="segmented" role="group" aria-label="${esc(title)}">${group}</div></div>`;
    html = `<div class="view-head"><h1>${tx('nav.settings')}</h1></div>
      <div class="panel">
        ${segRow(tx('set.appearance'), tx('set.appearanceHint'), themeBtn('auto', tx('theme.auto')) + themeBtn('light', tx('theme.light')) + themeBtn('dark', tx('theme.dark')))}
        ${segRow(tx('set.language'), tx('set.languageHint'), langBtn('auto', tx('theme.auto')) + Object.entries(NAMES).map(([k, n]) => langBtn(k, n)).join(''))}
        ${sw('hiRes', S.settings.hiRes, tx('set.hiRes'), tx('set.hiResHint'))}
        ${sw('onlineLyrics', S.onlineLyrics, tx('set.lyrics'), tx('set.lyricsHint'))}
        ${sw('startup', S.settings.startup, tx('set.startup'), '')}
        ${sw('pinned', S.settings.pinned, tx('set.pinned'), '')}
      </div>
      <h2 class="section-title">${tx('set.updates')}</h2>
      <div class="panel">
        <div class="row"><div class="grow"><div class="t1" style="white-space:normal">${S.update.current ? `Sleeve ${esc(S.update.current)}` : 'Sleeve'}</div>
          <div class="t2" style="white-space:normal;font-size:13px">${updateText()}</div></div>
          ${S.update.state === 'ready'
            ? `<button type="button" class="btn primary" data-act="update-restart">${tx('set.restart')}</button>`
            : `<button type="button" class="btn" data-act="update-check"${['checking', 'downloading'].includes(S.update.state) ? ' disabled' : ''}>${I.refresh}${tx('set.check')}</button>`}
        </div>
        ${sw('autoUpdate', S.settings.autoUpdate, tx('set.autoUpdate'), tx('set.autoUpdateHint'))}
      </div>
      <p class="note">${tx('set.shortcuts')}</p>`;
  } else if (v.name === 'search') {
    const q = norm(S.search.trim());
    const albums = S.albums.filter((a) => norm(a.title).includes(q) || norm(artistOf(a)).includes(q));
    const tracks = S.allTracks.filter((t) => norm(t.title).includes(q) || norm(t.artist).includes(q)).slice(0, 200);
    html = `<div class="view-head"><h1>${tx('search.results')}</h1></div>`
      + (albums.length ? albumGrid(albums.slice(0, 24)) : '')
      + (tracks.length ? `<h2 class="section-title">${tx('nav.tracks')}</h2>${trackList('search', tracks)}` : '')
      + (!albums.length && !tracks.length ? `<div class="empty"><p>${tx('search.none', { q: esc(S.search) })}</p></div>` : '');
  }
  el.innerHTML = html;
}

function updateText() {
  const u = S.update;
  if (u.state === 'checking') return tx('upd.checking');
  if (u.state === 'downloading') return tx('upd.downloading', { v: esc(u.version) });
  if (u.state === 'ready') return tx('upd.ready', { v: esc(u.version) });
  if (u.state === 'none') return tx('upd.none');
  if (u.state === 'error') return tx('upd.error');
  return tx('upd.idle');
}
function renderUpdateChip() {
  const chip = $('#update-chip');
  const ready = S.update.state === 'ready';
  chip.hidden = !ready;
  chip.textContent = ready ? tx('upd.chip', { v: S.update.version }) : '';
}

// ---------- Desenho: barra, tocando agora e mini ----------
function slider(cls, ratio) {
  const p = Math.max(0, Math.min(1, ratio || 0)) * 100;
  return `<div class="slider ${cls}" role="slider" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${Math.round(p)}" tabindex="0">
    <div class="rail"></div><div class="fillbar" style="width:${p}%"></div><div class="knob" style="left:${p}%"></div></div>`;
}
function controlsHtml(i, big) {
  const local = i.mode === 'local';
  return `<div class="controls">
    ${local ? `<button type="button" class="ctl small ${S.shuffle ? 'active' : 'dim'}" data-act="shuffle" aria-label="${tx('ctl.shuffle')}" aria-pressed="${S.shuffle}">${I.shuffle}</button>` : ''}
    <button type="button" class="ctl" data-act="prev" aria-label="${tx('ctl.prev')}"${i.canPrevious ? '' : ' disabled'}>${I.prev}</button>
    <button type="button" class="ctl play" data-act="toggle" aria-label="${i.playing ? tx('ctl.pause') : tx('ctl.play')}">${i.playing ? I.pause : I.play}</button>
    <button type="button" class="ctl" data-act="next" aria-label="${tx('ctl.next')}"${i.canNext ? '' : ' disabled'}>${I.next}</button>
    ${local ? `<button type="button" class="ctl small ${S.repeat !== 'off' ? 'active' : 'dim'}" data-act="repeat" aria-label="${tx(S.repeat === 'one' ? 'ctl.repeatOne' : S.repeat === 'all' ? 'ctl.repeatAll' : 'ctl.repeatOff')}">${S.repeat === 'one' ? I.repeatOne : I.repeat}</button>` : ''}
    ${big && local ? `<button type="button" class="ctl small like" data-act="like" aria-label="${i.liked ? tx('ctl.unlike') : tx('ctl.like')}" aria-pressed="${!!i.liked}">${i.liked ? I.heartOn : I.heart}</button>` : ''}
  </div>`;
}

let lastTrackKey = '';
function renderPlayback(changed = false) {
  const i = info();
  const key = i ? `${i.mode}|${i.title}|${i.artist}|${i.album}` : '';
  const swap = key !== lastTrackKey;
  lastTrackKey = key;
  applyColors(i?.color);

  // Barra inferior
  const bar = $('#bar');
  bar.hidden = !i;
  if (i) {
    const vol = i.mode === 'spotify' ? (S.spotify.volume ?? 1) : (audio.muted ? 0 : S.volume);
    bar.innerHTML = `
      <button type="button" class="bar-track" data-act="open-now" aria-label="${tx('ctl.openNow')}">
        ${coverHtml(i.cover, i.album)}
        <span class="meta${swap ? ' swap' : ''}"><span class="t1">${esc(i.title)}</span><span class="t2">${esc([i.artist, i.album].filter(Boolean).join(' · '))}</span></span>
      </button>
      <div class="bar-center">${controlsHtml(i, false)}
        <div class="progress"><span class="js-pos">${fmt(i.position)}</span>${slider('js-seek', i.duration ? i.position / i.duration : 0)}<span class="js-dur">${fmt(i.duration)}</span></div>
      </div>
      <div class="bar-right">
        <button type="button" class="ctl small dim" data-act="mute" aria-label="${tx('ctl.volume')}">${vol > 0 ? I.volume : I.mute}</button>
        ${slider('js-vol', vol)}
        <button type="button" class="ctl small" data-act="open-now" aria-label="${tx('ctl.openNow')}">${I.expand}</button>
      </div>`;
  }

  renderNow(i, swap);
  renderMini(i);
  updateMediaSession(i);
  if (changed || swap) {
    // Destaca a faixa atual nas listas abertas
    const cur = i?.mode === 'local' ? current()?.id : null;
    document.querySelectorAll('.track').forEach((li) => {
      const list = S.lists[li.dataset.list];
      const t = list?.[+li.dataset.i];
      li.classList.toggle('playing', !!cur && t?.id === cur);
    });
  }
}

// Teclas de mídia do teclado e painel de mídia do sistema (quando o sistema oferece)
let mediaReady = false;
function updateMediaSession(i) {
  if (!('mediaSession' in navigator)) return;
  try {
    if (!mediaReady) {
      mediaReady = true;
      navigator.mediaSession.setActionHandler('play', () => { if (mode() === 'local') audio.play().catch(() => {}); });
      navigator.mediaSession.setActionHandler('pause', () => { if (mode() === 'local') audio.pause(); });
      navigator.mediaSession.setActionHandler('nexttrack', () => next());
      navigator.mediaSession.setActionHandler('previoustrack', () => previous());
    }
    if (i?.mode === 'local') {
      navigator.mediaSession.metadata = new MediaMetadata({
        title: i.title, artist: i.artist, album: i.album,
        artwork: i.cover ? [{ src: i.cover, sizes: '512x512' }] : [],
      });
    }
  } catch { /* sem suporte */ }
}

function renderNow(i, swap) {
  const el = $('#now');
  el.hidden = !S.nowOpen;
  if (!S.nowOpen) return;
  const upcoming = i?.mode === 'local' ? S.queue.slice(S.index + 1, S.index + 51) : [];
  const showLyrics = !!i && S.lyricsOn;
  if (showLyrics) wantLyrics(i);
  // A letra que já está na tela continua a mesma (sem perder a rolagem) quando só o tempo mudou
  const keep = el.querySelector('.lyr-scroll');
  el.innerHTML = `
    <div class="bg" style="${i?.cover ? `background-image:url('${esc(i.cover)}')` : ''}"></div>
    <div class="veil"></div>
    <div class="layer">
      <div class="now-top drag">
        <button type="button" class="pill-btn" data-act="close-now">${I.down}${tx('now.library')}</button>
        ${i ? `<button type="button" class="pill-btn lyr-btn${S.lyricsOn ? ' on' : ''}" data-act="lyrics" aria-pressed="${S.lyricsOn}">${I.lyrics}${tx('now.lyrics')}</button>` : ''}
        <span class="now-source">${i ? `${i.playing ? tx('now.playing') : tx('now.paused')} · ${esc(i.source)}` : tx('now.nothing')}</span>
        <div class="win-buttons" style="margin-left:8px">
          <button type="button" class="win" data-win="mini" aria-label="${tx('win.mini')}">${I.mini}</button>
          <button type="button" class="win" data-win="minimize" aria-label="${tx('win.minimize')}">${I.minimize}</button>
          <button type="button" class="win close" data-win="close" aria-label="${tx('win.close')}">${I.close}</button>
        </div>
      </div>
      <div class="now-main${showLyrics ? ' with-lyrics' : ''}">
        <div class="now-cover">${i?.cover ? `<img src="${esc(i.cover)}" alt="">` : MARK}</div>
        <div class="now-info${swap ? ' swap' : ''}">
          <h1>${esc(i?.title || tx('now.nothing'))}</h1>
          <span class="artist">${esc(i?.artist || tx('now.hint'))}</span>
          <span class="album">${esc(i?.album || '')}</span>
          ${i ? `<div class="progress">${slider('js-seek', i.duration ? i.position / i.duration : 0)}
              <div class="times"><span class="js-pos">${fmt(i.position)}</span><span class="js-dur">${fmt(i.duration)}</span></div></div>
            ${controlsHtml(i, true)}` : ''}
        </div>
        ${showLyrics ? `<aside class="lyrics-panel" aria-label="${tx('now.lyrics')}">${lyricsHtml()}</aside>`
          : i?.mode === 'local' ? `<aside class="queue" aria-label="${tx('now.upNext')}"><h2>${tx('now.upNext')}</h2>
          ${upcoming.length ? upcoming.map((t, k) => {
            const a = S.albumById.get(t.albumId);
            const n = S.index + 1 + k;
            return `<div class="q" data-act="jump" data-i="${n}" draggable="true" role="button" tabindex="0" title="${tx('queue.dragHint')}">${coverHtml(a?.cover)}
              <span class="q-text"><span class="t1" style="font-size:14px">${esc(t.title)}</span><span class="t2">${esc(t.artist || a?.artist || '')}</span></span>
              <button type="button" class="q-x" data-act="dequeue" data-i="${n}" aria-label="${tx('queue.remove')}">${I.close}</button></div>`;
          }).join('') : `<span class="empty-q">${tx('now.endOfQueue')}</span>`}
        </aside>` : ''}
      </div>
    </div>`;
  const fresh = el.querySelector('.lyr-scroll');
  if (keep && fresh && keep.dataset.key === fresh.dataset.key) fresh.replaceWith(keep);
  else if (fresh) paintLyrics(i?.position || 0, true);
}

function renderMini(i) {
  $('#mini').innerHTML = `
    <div class="bg" style="${i?.cover ? `background-image:url('${esc(i.cover)}')` : ''}"></div><div class="veil"></div>
    ${coverHtml(i?.cover)}
    <div class="meta"><span class="t1">${esc(i?.title || tx('now.nothing'))}</span><span class="t2">${esc(i?.artist || 'Sleeve')}</span></div>
    ${i ? `<button type="button" class="ctl" data-act="prev" aria-label="${tx('ctl.prev')}"${i.canPrevious ? '' : ' disabled'}>${I.prev}</button>
    <button type="button" class="ctl play" data-act="toggle" aria-label="${i.playing ? tx('ctl.pause') : tx('ctl.play')}">${i.playing ? I.pause : I.play}</button>
    <button type="button" class="ctl" data-act="next" aria-label="${tx('ctl.next')}"${i.canNext ? '' : ' disabled'}>${I.next}</button>` : ''}
    <button type="button" class="ctl small" data-win="mini" aria-label="${tx('win.expand')}">${I.expand}</button>
    <div class="line js-line" style="width:${i?.duration ? (i.position / i.duration) * 100 : 0}%"></div>`;
}

function renderVolume() {
  const i = info();
  if (!i) return;
  const vol = i.mode === 'spotify' ? (S.spotify.volume ?? 1) : (audio.muted ? 0 : S.volume);
  document.querySelectorAll('.js-vol').forEach((s) => {
    s.querySelector('.fillbar').style.width = vol * 100 + '%';
    s.querySelector('.knob').style.left = vol * 100 + '%';
  });
}

// Atualiza só o tempo e as barras, várias vezes por segundo
let lastTick = 0;
function tick(force = false) {
  const now = performance.now();
  if (!force && now - lastTick < 250) return;
  lastTick = now;
  const i = info();
  if (!i) return;
  const r = i.duration ? Math.min(1, i.position / i.duration) : 0;
  document.querySelectorAll('.js-pos').forEach((e) => { e.textContent = fmt(i.position); });
  document.querySelectorAll('.js-dur').forEach((e) => { e.textContent = fmt(i.duration); });
  document.querySelectorAll('.js-seek').forEach((s) => {
    if (s === dragging) return;
    s.querySelector('.fillbar').style.width = r * 100 + '%';
    s.querySelector('.knob').style.left = r * 100 + '%';
  });
  document.querySelectorAll('.js-line').forEach((e) => { e.style.width = r * 100 + '%'; });
  if (S.nowOpen && S.lyricsOn) paintLyrics(i.position);
}
(function loop() { tick(); requestAnimationFrame(loop); })();

function renderAll() {
  renderNav();
  renderView();
  renderPlayback(true);
  $('#pin-button').classList.toggle('on', !!S.settings.pinned);
}

// ---------- Cliques ----------
document.addEventListener('click', (e) => {
  const win = e.target.closest('[data-win]');
  if (win) { host.send({ type: 'window', action: win.dataset.win }); return; }
  const navBtn = e.target.closest('[data-view]');
  if (navBtn) { go({ name: navBtn.dataset.view }); return; }
  const el = e.target.closest('[data-act]');
  if (!el) return;
  const d = el.dataset;
  switch (d.act) {
    case 'album': go({ name: 'album', id: d.id }); break;
    case 'artist': go({ name: 'artist', id: d.name }); break;
    case 'back': goBack(); break;
    case 'sort': S.sort = d.sort; save('sort', S.sort); renderView(); break;
    case 'play-album': { const a = S.albumById.get(d.id); if (a) playList(a.tracks, 0); break; }
    case 'shuffle-album': { const a = S.albumById.get(d.id); if (a) playList(a.tracks, 0, true); break; }
    case 'play-list': playList(listFor(d.list), 0); break;
    case 'shuffle-list': playList(listFor(d.list), 0, true); break;
    case 'toggle': cmd.toggle(); break;
    case 'next': cmd.next(); break;
    case 'prev': cmd.previous(); break;
    case 'shuffle': toggleShuffle(); break;
    case 'repeat': cycleRepeat(); break;
    case 'like': cmd.like(); break;
    case 'mute': audio.muted = !audio.muted; renderPlayback(); break;
    case 'jump': S.index = +d.i; loadCurrent(true); break;
    case 'dequeue': dequeue(+d.i); break;
    case 'lyrics': S.lyricsOn = !S.lyricsOn; save('lyricsOn', S.lyricsOn); renderPlayback(); break;
    case 'lyr-seek': {
      const i = info();
      if (i?.canSeek && i.duration) { lyrUserUntil = 0; cmd.seek(+d.t / i.duration); }
      break;
    }
    case 'open-now': openNow(true); break;
    case 'close-now': openNow(false); break;
    case 'add-folder': host.send({ type: 'folders', action: 'add' }); break;
    case 'remove-folder': host.send({ type: 'folders', action: 'remove', path: d.path }); break;
    case 'rescan': host.send({ type: 'folders', action: 'rescan' }); break;
    case 'update-check': host.send({ type: 'update', action: 'check' }); break;
    case 'update-restart': host.send({ type: 'update', action: 'restart' }); break;
    case 'theme': S.theme = d.theme; save('theme', S.theme); applyTheme(); renderView(); break;
    case 'lang': S.lang = d.lang; save('lang', S.lang); applyLang(); renderAll(); break;
    case 'setting': {
      if (d.key === 'onlineLyrics') {
        S.onlineLyrics = !S.onlineLyrics; save('onlineLyrics', S.onlineLyrics);
        el.setAttribute('aria-checked', String(S.onlineLyrics));
        lyricCache.clear(); S.lyr = { key: '', state: 'idle', lines: null, plain: '' };
        break;
      }
      const val = !S.settings[d.key];
      S.settings[d.key] = val;
      el.setAttribute('aria-checked', String(val));
      host.send({ type: 'setting', key: d.key, value: val });
      break;
    }
  }
});
// Faixas: clique duplo toca (clique simples só seleciona, como num player de PC)
document.addEventListener('dblclick', (e) => {
  const li = e.target.closest('[data-act="play"]');
  if (li) { playList(S.lists[li.dataset.list] || [], +li.dataset.i); return; }
  const drag = e.target.closest('.drag');
  if (drag && !e.target.closest('button, input')) host.send({ type: 'window', action: 'maximize' });
});
document.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') {
    const li = e.target.closest?.('[data-act="play"]');
    if (li) playList(S.lists[li.dataset.list] || [], +li.dataset.i);
    const q = e.target.closest?.('.queue .q');
    if (q && e.target === q) { S.index = +q.dataset.i; loadCurrent(true); }
  }
});
function listFor(key) {
  if (key === 'all') return S.allTracks;
  if (key === 'likes') return S.allTracks.filter((t) => S.likes.has(t.id));
  return S.lists[key] || [];
}
let enterTimer = 0;
function openNow(open) {
  S.nowOpen = open;
  const el = $('#now');
  // A animação de entrada roda só ao abrir, não a cada atualização da tela
  el.classList.toggle('enter', open);
  clearTimeout(enterTimer);
  if (open) enterTimer = setTimeout(() => el.classList.remove('enter'), 400);
  renderPlayback();
}

// Barras de progresso e volume: clicar ou arrastar
function sliderRatio(s, x) {
  const r = s.getBoundingClientRect();
  return Math.min(1, Math.max(0, (x - r.left) / r.width));
}
function paintSlider(s, ratio) {
  s.querySelector('.fillbar').style.width = ratio * 100 + '%';
  s.querySelector('.knob').style.left = ratio * 100 + '%';
}
document.addEventListener('pointerdown', (e) => {
  const edge = e.target.closest('.edge');
  if (edge && e.button === 0) { host.send({ type: 'window', action: 'resize', edge: edge.dataset.edge }); return; }
  const s = e.target.closest('.slider');
  if (s && e.button === 0) {
    dragging = s;
    s.setPointerCapture(e.pointerId);
    const r = sliderRatio(s, e.clientX);
    paintSlider(s, r);
    if (s.classList.contains('js-vol')) cmd.volume(r);
    e.preventDefault();
    return;
  }
  const drag = e.target.closest('.drag');
  if (drag && e.button === 0 && !e.target.closest('button, input, .slider')) {
    host.send({ type: 'window', action: 'drag' });
  }
});
document.addEventListener('pointermove', (e) => {
  if (!dragging) return;
  const r = sliderRatio(dragging, e.clientX);
  paintSlider(dragging, r);
  if (dragging.classList.contains('js-vol')) cmd.volume(r);
});
document.addEventListener('pointerup', (e) => {
  if (!dragging) return;
  const s = dragging;
  dragging = null;
  if (s.classList.contains('js-seek')) cmd.seek(sliderRatio(s, e.clientX));
});

// Roda do mouse sobre a barra ou o tocando agora: volume
document.addEventListener('wheel', (e) => {
  if (!e.target.closest('.bar, .now, .mini')) return;
  if (e.target.closest('.queue')) return;
  if (e.target.closest('.lyrics-panel')) { lyrUserUntil = performance.now() + 4000; return; }
  const i = info();
  if (!i) return;
  const cur = i.mode === 'spotify' ? (S.spotify.volume ?? 1) : S.volume;
  cmd.volume(cur + (e.deltaY < 0 ? 0.04 : -0.04));
  e.preventDefault();
}, { passive: false });

// Busca
$('#search').addEventListener('input', (e) => {
  S.search = e.target.value;
  if (S.search.trim()) {
    if (S.view.name !== 'search') S.history.push(S.view);
    S.view = { name: 'search' };
    renderNav();
    renderView();
  } else if (S.view.name === 'search') goBack();
});

// Atalhos
document.addEventListener('keydown', (e) => {
  const typing = e.target.closest?.('input');
  if (e.ctrlKey && e.key.toLowerCase() === 'f') { $('#search').focus(); e.preventDefault(); return; }
  if (typing) { if (e.key === 'Escape') { e.target.value = ''; e.target.dispatchEvent(new Event('input')); e.target.blur(); } return; }
  if (e.key === ' ') { cmd.toggle(); e.preventDefault(); }
  else if (e.key === 'ArrowRight') { cmd.next(); e.preventDefault(); }
  else if (e.key === 'ArrowLeft') { cmd.previous(); e.preventDefault(); }
  else if (e.key.toLowerCase() === 'm') host.send({ type: 'window', action: 'mini' });
  else if (e.key.toLowerCase() === 't') host.send({ type: 'window', action: 'pin' });
  else if (e.key === 'Escape' && document.getElementById('ctx')) closeMenu();
  else if (e.key === 'Escape' && S.nowOpen) openNow(false);
  else if (e.key.toLowerCase() === 'l' && S.nowOpen) { S.lyricsOn = !S.lyricsOn; save('lyricsOn', S.lyricsOn); renderPlayback(); }
  else if (e.key === 'Backspace' && S.history.length) goBack();
});

// ---------- Mensagens do sistema ----------
host.on((msg) => {
  if (!msg || typeof msg !== 'object') return;
  switch (msg.type) {
    case 'library': setLibrary(msg); break;
    case 'scan': S.scanning = true; S.scanCount = msg.count; renderNav(); if (S.view.name === 'folders' || !S.albums.length) renderView(); break;
    case 'settings':
      Object.assign(S.settings, msg);
      $('#pin-button').classList.toggle('on', !!S.settings.pinned);
      if (S.view.name === 'ajustes') renderView();
      break;
    case 'spotify': {
      const was = S.spotify;
      S.spotify = msg.info ? { ...msg.info } : null;
      S.spotifyAt = performance.now();
      // Deu play no Spotify enquanto um arquivo tocava: pausa o arquivo
      if (S.spotify?.playing && !(was?.playing) && !audio.paused) audio.pause();
      $('#spotify-dot').classList.toggle('on', !!S.spotify);
      renderSpotifyState();
      renderPlayback();
      break;
    }
    case 'update':
      S.update = { state: msg.state, version: msg.version, current: msg.current };
      renderUpdateChip();
      if (S.view.name === 'ajustes') renderView();
      break;
    case 'open': openFiles(msg.tracks || []); break;
    case 'lyrics': gotLyrics(msg); break;
    case 'media-key':
      if (msg.key === 'toggle') cmd.toggle();
      else if (msg.key === 'next') cmd.next();
      else if (msg.key === 'previous') cmd.previous();
      break;
  }
});

// Para testar o visual num navegador comum: ?tela=tocando abre o tocando agora
const params = new URLSearchParams(location.search);
const tela = params.get('tela');
if (params.get('tema')) S.theme = params.get('tema');
if (params.get('idioma')) S.lang = params.get('idioma');
if (params.get('letra')) S.lyricsOn = params.get('letra') === '1';
applyTheme();
applyLang();
renderAll();
host.send({ type: 'ready' });
if (tela === 'tocando') setTimeout(() => openNow(true), 300);
if (tela === 'album') setTimeout(() => go({ name: 'album', id: 'demo-0' }), 300);
if (tela === 'fila') setTimeout(() => { const a = S.albumById.get('demo-0'); if (a) { S.baseQueue = a.tracks; S.queue = [...a.tracks]; S.index = 2; renderPlayback(true); go({ name: 'album', id: 'demo-0' }); openNow(true); } }, 300);
