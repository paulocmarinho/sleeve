// Idiomas da interface. Para adicionar um idioma: copie o bloco "en",
// traduza os textos e acrescente o código em LANGS e em NAMES.

const pt = {
  'lib.continue': 'Continuar ouvindo', 'lib.allAlbums': 'Todos os álbuns', 'artist.eyebrow': 'Artista',
  'set.artistPhotos': 'Fotos de artistas', 'set.artistPhotosHint': 'Usa o artist.jpg da pasta do artista; se não tiver, busca a foto no Deezer. Envia o nome do artista.',
  'play.error': 'Não deu para tocar "{t}".',
  'now.lyrics': 'Letra', 'lyrics.loading': 'Procurando a letra…', 'lyrics.none': 'Não achei a letra desta música.',
  'queue.playNext': 'Tocar a seguir', 'queue.add': 'Adicionar à fila', 'queue.remove': 'Tirar da fila',
  'queue.toastNext': '"{t}" toca a seguir', 'queue.toastAdd': '"{t}" foi para a fila', 'queue.dragHint': 'Arraste para mudar a ordem',
  'menu.goAlbum': 'Ir para o álbum', 'menu.goArtist': 'Ir para o artista',
  'set.lyrics': 'Buscar letras na internet', 'set.lyricsHint': 'Quando a música não traz a letra, procura no LRCLIB. Envia o nome da música e do artista.',
  'nav.albums': 'Álbuns', 'nav.artists': 'Artistas', 'nav.tracks': 'Músicas', 'nav.likes': 'Curtidas', 'nav.folders': 'Pastas', 'nav.settings': 'Ajustes',
  'src.title': 'Fontes', 'src.files': 'Arquivos', 'src.reading': 'lendo…', 'src.readingN': 'lendo… {n}', 'src.tracksN': '{n} faixas',
  'sp.closed': 'fechado', 'sp.connected': 'conectado', 'sp.playing': 'tocando',
  'search.placeholder': 'Buscar música, artista ou álbum', 'search.label': 'Buscar',
  'win.mini': 'Mini player (M)', 'win.pin': 'Sempre no topo (T)', 'win.minimize': 'Minimizar', 'win.maximize': 'Maximizar', 'win.close': 'Fechar (continua na bandeja)', 'win.expand': 'Expandir',
  'lib.title': 'Sua biblioteca', 'lib.reading': 'Lendo sua biblioteca…', 'lib.empty': 'Sua biblioteca está vazia',
  'lib.foundSoFar': '{n} faixas encontradas até agora.', 'lib.emptyHint': 'Escolha a pasta onde ficam suas músicas. O Sleeve lê as faixas, os álbuns e as capas sozinho.',
  'lib.addFolder': 'Adicionar pasta', 'lib.unknownArtist': 'Artista desconhecido',
  'sort.recent': 'Adicionados recentemente', 'sort.az': 'A a Z', 'sort.artist': 'Artista', 'sort.plays': 'Mais tocados',
  'common.back': 'Voltar', 'common.play': 'Tocar', 'common.shuffle': 'Aleatório', 'common.playAll': 'Tocar tudo',
  'album.eyebrow': 'Álbum', 'album.track1': '{n} faixa', 'album.tracksN': '{n} faixas', 'album.album1': '{n} álbum', 'album.albumsN': '{n} álbuns',
  'track.hint': 'Clique duas vezes para tocar',
  'likes.empty': 'As músicas que você curtir aparecem aqui. Use o coração no "tocando agora".',
  'folders.remove': 'Tirar a pasta {path}', 'folders.none': 'Nenhuma pasta ainda.', 'folders.rescan': 'Ler de novo', 'folders.readingN': 'Lendo… {n}',
  'folders.note': 'O Sleeve lê MP3, FLAC, M4A/AAC, OGG, Opus e WAV dessas pastas e de todas as pastas dentro delas.',
  'set.appearance': 'Aparência', 'set.appearanceHint': 'Automático segue o tema do sistema.',
  'set.language': 'Idioma', 'set.languageHint': 'Automático segue o idioma do sistema.',
  'theme.auto': 'Automático', 'theme.light': 'Claro', 'theme.dark': 'Escuro',
  'set.hiRes': 'Capa em alta resolução', 'set.hiResHint': 'Busca a capa em 1000 px no iTunes quando a música não traz uma boa. Envia o nome do artista e do álbum para a Apple.',
  'set.startup': 'Iniciar com o Windows', 'set.pinned': 'Sempre no topo',
  'set.updates': 'Atualizações', 'set.restart': 'Reiniciar e atualizar', 'set.check': 'Procurar',
  'set.autoUpdate': 'Atualizar automaticamente', 'set.autoUpdateHint': 'Ao abrir, o Sleeve procura a versão mais nova no GitHub e baixa sozinho.',
  'set.shortcuts': 'Atalhos: espaço toca e pausa, ← e → trocam de faixa, M abre o mini player, L mostra a letra, Esc fecha o tocando agora. Botão direito numa música põe na fila.',
  'search.results': 'Resultados', 'search.none': 'Nada encontrado para "{q}".',
  'upd.checking': 'Procurando atualizações…', 'upd.downloading': 'Baixando a versão {v}…', 'upd.ready': 'A versão {v} está pronta. Ela entra quando o Sleeve reiniciar.',
  'upd.none': 'Você está na versão mais nova.', 'upd.error': 'Não deu para atualizar agora. Tente de novo mais tarde.', 'upd.idle': 'O Sleeve procura atualizações no GitHub.',
  'upd.chip': 'Atualizar para {v}',
  'ctl.shuffle': 'Aleatório', 'ctl.prev': 'Anterior', 'ctl.play': 'Tocar', 'ctl.pause': 'Pausar', 'ctl.next': 'Próxima',
  'ctl.repeatOff': 'Repetir: desligado', 'ctl.repeatAll': 'Repetir: tudo', 'ctl.repeatOne': 'Repetir: esta faixa',
  'ctl.like': 'Curtir', 'ctl.unlike': 'Descurtir', 'ctl.volume': 'Volume', 'ctl.openNow': 'Abrir tocando agora',
  'now.library': 'Biblioteca', 'now.playing': 'Tocando agora', 'now.paused': 'Pausado', 'now.nothing': 'Nada tocando',
  'now.hint': 'Escolha um álbum na biblioteca ou dê play no Spotify.', 'now.upNext': 'A seguir', 'now.endOfQueue': 'Fim da fila.',
};

const en = {
  'lib.continue': 'Continue listening', 'lib.allAlbums': 'All albums', 'artist.eyebrow': 'Artist',
  'set.artistPhotos': 'Artist photos', 'set.artistPhotosHint': "Uses artist.jpg from the artist's folder; if there is none, gets the photo from Deezer. Sends the artist name.",
  'play.error': 'Couldn\'t play "{t}".',
  'now.lyrics': 'Lyrics', 'lyrics.loading': 'Looking for the lyrics…', 'lyrics.none': "Couldn't find lyrics for this song.",
  'queue.playNext': 'Play next', 'queue.add': 'Add to queue', 'queue.remove': 'Remove from queue',
  'queue.toastNext': '"{t}" plays next', 'queue.toastAdd': '"{t}" added to the queue', 'queue.dragHint': 'Drag to reorder',
  'menu.goAlbum': 'Go to album', 'menu.goArtist': 'Go to artist',
  'set.lyrics': 'Find lyrics online', 'set.lyricsHint': "When a song has no lyrics, looks them up on LRCLIB. Sends the song and artist name.",
  'nav.albums': 'Albums', 'nav.artists': 'Artists', 'nav.tracks': 'Songs', 'nav.likes': 'Liked', 'nav.folders': 'Folders', 'nav.settings': 'Settings',
  'src.title': 'Sources', 'src.files': 'Files', 'src.reading': 'reading…', 'src.readingN': 'reading… {n}', 'src.tracksN': '{n} songs',
  'sp.closed': 'closed', 'sp.connected': 'connected', 'sp.playing': 'playing',
  'search.placeholder': 'Search songs, artists or albums', 'search.label': 'Search',
  'win.mini': 'Mini player (M)', 'win.pin': 'Always on top (T)', 'win.minimize': 'Minimize', 'win.maximize': 'Maximize', 'win.close': 'Close (keeps running in the tray)', 'win.expand': 'Expand',
  'lib.title': 'Your library', 'lib.reading': 'Reading your library…', 'lib.empty': 'Your library is empty',
  'lib.foundSoFar': '{n} songs found so far.', 'lib.emptyHint': 'Choose the folder where your music lives. Sleeve finds the songs, albums and covers on its own.',
  'lib.addFolder': 'Add folder', 'lib.unknownArtist': 'Unknown artist',
  'sort.recent': 'Recently added', 'sort.az': 'A to Z', 'sort.artist': 'Artist', 'sort.plays': 'Most played',
  'common.back': 'Back', 'common.play': 'Play', 'common.shuffle': 'Shuffle', 'common.playAll': 'Play all',
  'album.eyebrow': 'Album', 'album.track1': '{n} song', 'album.tracksN': '{n} songs', 'album.album1': '{n} album', 'album.albumsN': '{n} albums',
  'track.hint': 'Double-click to play',
  'likes.empty': 'Songs you like show up here. Use the heart on the now playing screen.',
  'folders.remove': 'Remove folder {path}', 'folders.none': 'No folders yet.', 'folders.rescan': 'Scan again', 'folders.readingN': 'Reading… {n}',
  'folders.note': 'Sleeve reads MP3, FLAC, M4A/AAC, OGG, Opus and WAV from these folders and every folder inside them.',
  'set.appearance': 'Appearance', 'set.appearanceHint': 'Automatic follows the system theme.',
  'set.language': 'Language', 'set.languageHint': 'Automatic follows the system language.',
  'theme.auto': 'Automatic', 'theme.light': 'Light', 'theme.dark': 'Dark',
  'set.hiRes': 'High-resolution covers', 'set.hiResHint': 'Gets a 1000 px cover from iTunes when a song has no good one. Sends the artist and album name to Apple.',
  'set.startup': 'Start with Windows', 'set.pinned': 'Always on top',
  'set.updates': 'Updates', 'set.restart': 'Restart and update', 'set.check': 'Check',
  'set.autoUpdate': 'Update automatically', 'set.autoUpdateHint': 'When it opens, Sleeve looks for the newest version on GitHub and downloads it on its own.',
  'set.shortcuts': 'Shortcuts: space plays and pauses, ← and → change songs, M opens the mini player, L shows lyrics, Esc closes now playing. Right-click a song to queue it.',
  'search.results': 'Results', 'search.none': 'Nothing found for "{q}".',
  'upd.checking': 'Checking for updates…', 'upd.downloading': 'Downloading version {v}…', 'upd.ready': 'Version {v} is ready. It takes effect when Sleeve restarts.',
  'upd.none': 'You have the latest version.', 'upd.error': "Couldn't update right now. Try again later.", 'upd.idle': 'Sleeve checks for updates on GitHub.',
  'upd.chip': 'Update to {v}',
  'ctl.shuffle': 'Shuffle', 'ctl.prev': 'Previous', 'ctl.play': 'Play', 'ctl.pause': 'Pause', 'ctl.next': 'Next',
  'ctl.repeatOff': 'Repeat: off', 'ctl.repeatAll': 'Repeat: all', 'ctl.repeatOne': 'Repeat: this song',
  'ctl.like': 'Like', 'ctl.unlike': 'Unlike', 'ctl.volume': 'Volume', 'ctl.openNow': 'Open now playing',
  'now.library': 'Library', 'now.playing': 'Now playing', 'now.paused': 'Paused', 'now.nothing': 'Nothing playing',
  'now.hint': 'Pick an album from your library or press play on Spotify.', 'now.upNext': 'Up next', 'now.endOfQueue': 'End of queue.',
};

const es = {
  'lib.continue': 'Seguir escuchando', 'lib.allAlbums': 'Todos los álbumes', 'artist.eyebrow': 'Artista',
  'set.artistPhotos': 'Fotos de artistas', 'set.artistPhotosHint': 'Usa el artist.jpg de la carpeta del artista; si no hay, busca la foto en Deezer. Envía el nombre del artista.',
  'play.error': 'No se pudo reproducir "{t}".',
  'now.lyrics': 'Letra', 'lyrics.loading': 'Buscando la letra…', 'lyrics.none': 'No encontré la letra de esta canción.',
  'queue.playNext': 'Reproducir a continuación', 'queue.add': 'Añadir a la cola', 'queue.remove': 'Quitar de la cola',
  'queue.toastNext': '"{t}" suena a continuación', 'queue.toastAdd': '"{t}" se añadió a la cola', 'queue.dragHint': 'Arrastra para cambiar el orden',
  'menu.goAlbum': 'Ir al álbum', 'menu.goArtist': 'Ir al artista',
  'set.lyrics': 'Buscar letras en internet', 'set.lyricsHint': 'Cuando la canción no trae la letra, la busca en LRCLIB. Envía el nombre de la canción y del artista.',
  'nav.albums': 'Álbumes', 'nav.artists': 'Artistas', 'nav.tracks': 'Canciones', 'nav.likes': 'Favoritas', 'nav.folders': 'Carpetas', 'nav.settings': 'Ajustes',
  'src.title': 'Fuentes', 'src.files': 'Archivos', 'src.reading': 'leyendo…', 'src.readingN': 'leyendo… {n}', 'src.tracksN': '{n} canciones',
  'sp.closed': 'cerrado', 'sp.connected': 'conectado', 'sp.playing': 'sonando',
  'search.placeholder': 'Buscar canción, artista o álbum', 'search.label': 'Buscar',
  'win.mini': 'Minirreproductor (M)', 'win.pin': 'Siempre visible (T)', 'win.minimize': 'Minimizar', 'win.maximize': 'Maximizar', 'win.close': 'Cerrar (sigue en la bandeja)', 'win.expand': 'Expandir',
  'lib.title': 'Tu biblioteca', 'lib.reading': 'Leyendo tu biblioteca…', 'lib.empty': 'Tu biblioteca está vacía',
  'lib.foundSoFar': '{n} canciones encontradas hasta ahora.', 'lib.emptyHint': 'Elige la carpeta donde está tu música. Sleeve encuentra las canciones, los álbumes y las portadas solo.',
  'lib.addFolder': 'Añadir carpeta', 'lib.unknownArtist': 'Artista desconocido',
  'sort.recent': 'Añadidos recientemente', 'sort.az': 'A a Z', 'sort.artist': 'Artista', 'sort.plays': 'Más escuchados',
  'common.back': 'Volver', 'common.play': 'Reproducir', 'common.shuffle': 'Aleatorio', 'common.playAll': 'Reproducir todo',
  'album.eyebrow': 'Álbum', 'album.track1': '{n} canción', 'album.tracksN': '{n} canciones', 'album.album1': '{n} álbum', 'album.albumsN': '{n} álbumes',
  'track.hint': 'Doble clic para reproducir',
  'likes.empty': 'Las canciones que te gusten aparecen aquí. Usa el corazón en la pantalla de reproducción.',
  'folders.remove': 'Quitar la carpeta {path}', 'folders.none': 'Todavía no hay carpetas.', 'folders.rescan': 'Volver a leer', 'folders.readingN': 'Leyendo… {n}',
  'folders.note': 'Sleeve lee MP3, FLAC, M4A/AAC, OGG, Opus y WAV de estas carpetas y de todas las carpetas que contienen.',
  'set.appearance': 'Apariencia', 'set.appearanceHint': 'Automático sigue el tema del sistema.',
  'set.language': 'Idioma', 'set.languageHint': 'Automático sigue el idioma del sistema.',
  'theme.auto': 'Automático', 'theme.light': 'Claro', 'theme.dark': 'Oscuro',
  'set.hiRes': 'Portadas en alta resolución', 'set.hiResHint': 'Busca la portada en 1000 px en iTunes cuando la canción no trae una buena. Envía el nombre del artista y del álbum a Apple.',
  'set.startup': 'Iniciar con Windows', 'set.pinned': 'Siempre visible',
  'set.updates': 'Actualizaciones', 'set.restart': 'Reiniciar y actualizar', 'set.check': 'Buscar',
  'set.autoUpdate': 'Actualizar automáticamente', 'set.autoUpdateHint': 'Al abrirse, Sleeve busca la versión más nueva en GitHub y la descarga solo.',
  'set.shortcuts': 'Atajos: espacio reproduce y pausa, ← y → cambian de canción, M abre el minirreproductor, L muestra la letra, Esc cierra la reproducción. Clic derecho en una canción para ponerla en la cola.',
  'search.results': 'Resultados', 'search.none': 'No se encontró nada para "{q}".',
  'upd.checking': 'Buscando actualizaciones…', 'upd.downloading': 'Descargando la versión {v}…', 'upd.ready': 'La versión {v} está lista. Se aplica cuando Sleeve se reinicie.',
  'upd.none': 'Tienes la versión más reciente.', 'upd.error': 'No se pudo actualizar ahora. Inténtalo más tarde.', 'upd.idle': 'Sleeve busca actualizaciones en GitHub.',
  'upd.chip': 'Actualizar a {v}',
  'ctl.shuffle': 'Aleatorio', 'ctl.prev': 'Anterior', 'ctl.play': 'Reproducir', 'ctl.pause': 'Pausar', 'ctl.next': 'Siguiente',
  'ctl.repeatOff': 'Repetir: desactivado', 'ctl.repeatAll': 'Repetir: todo', 'ctl.repeatOne': 'Repetir: esta canción',
  'ctl.like': 'Me gusta', 'ctl.unlike': 'Ya no me gusta', 'ctl.volume': 'Volumen', 'ctl.openNow': 'Abrir reproducción',
  'now.library': 'Biblioteca', 'now.playing': 'Sonando ahora', 'now.paused': 'En pausa', 'now.nothing': 'Nada sonando',
  'now.hint': 'Elige un álbum de tu biblioteca o dale play en Spotify.', 'now.upNext': 'A continuación', 'now.endOfQueue': 'Fin de la cola.',
};

export const LANGS = { 'pt-BR': pt, en, es };
/** Nome de cada idioma escrito nele mesmo, para a lista em Ajustes. */
export const NAMES = { 'pt-BR': 'Português', en: 'English', es: 'Español' };

let dict = pt;
export let lang = 'pt-BR';

/** "auto" escolhe pelo idioma do sistema; sem tradução para ele, usa inglês. */
export function resolveLang(choice) {
  if (choice && choice !== 'auto' && LANGS[choice]) return choice;
  const sys = (navigator.languages?.[0] || navigator.language || 'en').toLowerCase();
  if (sys.startsWith('pt')) return 'pt-BR';
  if (sys.startsWith('es')) return 'es';
  return 'en';
}

export function setLang(code) {
  lang = LANGS[code] ? code : 'en';
  dict = LANGS[lang];
  document.documentElement.lang = lang;
}

/** Texto traduzido; {n}, {v}… são trocados pelos valores. Sem tradução, cai no português. */
export function t(key, vars) {
  let s = dict[key] ?? pt[key] ?? key;
  if (vars) for (const [k, v] of Object.entries(vars)) s = s.split(`{${k}}`).join(String(v));
  return s;
}
