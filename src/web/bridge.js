// Ponte entre a interface e o sistema.
//
// A interface só conversa por mensagens: manda pedidos com host.send(...)
// e recebe novidades com host.on(...). Cada sistema tem a sua ponte:
//   - Windows: o app em C# (WebView2), via window.chrome.webview
//   - Alba OS: (próxima etapa) o servidor do sistema
//   - Navegador comum: dados de exemplo, para testar o visual
//
// Mensagens que a interface manda:
//   {type:'ready'}
//   {type:'window', action:'drag'|'resize'|'minimize'|'maximize'|'close'|'mini'|'pin', edge?}
//   {type:'spotify', action:'toggle'|'pause'|'next'|'previous'|'seek', position?}
//   {type:'volume', app, value}             valor de 0 a 1
//   {type:'local', title, artist, playing}  o que o próprio Sleeve está tocando
//   {type:'folders', action:'add'|'remove'|'rescan', path?}
//   {type:'setting', key, value}
//   {type:'lyrics', key, title, artist, album, duration, url, online}  pede a letra; volta {type:'lyrics', key, synced, plain}
//   {type:'repair', id, url}               arquivo não abriu; volta {type:'repaired', id, url|null} com uma cópia tocável
//   {type:'artistPhotos', online, artists:[{name, albums, tracks, urls}]}  volta um {type:'artistPhoto', name, url|null} por artista
//   {type:'playstate', playing, canPrevious, canNext}  para os botões da barra de tarefas
//   {type:'theme', dark}                  tema da interface, para a janela acompanhar
//   {type:'language', lang}               idioma da interface (pt-BR, en, es), para a bandeja e os avisos
//   {type:'update', action:'check'|'restart'|'page'}
//
// Mensagens que a interface recebe:
//   {type:'library', albums:[…], folders:[…], scanning}
//   {type:'scan', count}
//   {type:'spotify', info:{…}|null}
//   {type:'settings', hiRes, startup, pinned, mini}
//   {type:'update', state:'idle'|'checking'|'downloading'|'ready'|'none'|'error', version, current}
//   {type:'open', tracks:[{id,title,artist,album,cover,color,duration,url}]}  músicas abertas pelo sistema
//   {type:'media-key', key:'toggle'|'next'|'previous'}

function windowsHost() {
  const wv = window.chrome.webview;
  return {
    kind: 'windows',
    send: (msg) => wv.postMessage(msg),
    on: (fn) => wv.addEventListener('message', (e) => fn(e.data)),
  };
}

function demoHost() {
  const listeners = [];
  return {
    kind: 'demo',
    send: async (msg) => {
      if (msg.type === 'ready') {
        const { demoLibrary, demoSpotify } = await import('./demo.js');
        const emit = (m) => listeners.forEach((fn) => fn(m));
        emit({ type: 'settings', hiRes: true, startup: false, pinned: false, mini: false, autoUpdate: true });
        const up = new URLSearchParams(location.search).get('atualizacao');
        emit({ type: 'update', state: up ? 'ready' : 'none', version: up ? '0.7.1' : null, current: '0.7.0' });
        emit({ type: 'library', ...demoLibrary(), scanning: false });
        if (new URLSearchParams(location.search).get('abrir')) {
          emit({ type: 'open', tracks: [{ id: 'arquivo-1', title: 'Good Morning', artist: 'Kanye West', album: 'Graduation', cover: null, color: '#8E44AD', duration: 195, url: '' }] });
          emit({ type: 'library', ...demoLibrary(), scanning: false }); // a biblioteca atualiza depois: a música aberta continua
        }
        const faixas = new URLSearchParams(location.search).get('faixas'); // ?faixas=a.flac,b.flac: arquivos de teste
        if (faixas) emit({ type: 'open', tracks: faixas.split(',').map((u, k) => ({ id: 'teste-' + k, title: 'Teste ' + (k + 1), artist: 'Sleeve', album: 'Teste', cover: null, color: '#7C3AED', duration: 0, url: u })) });
        const sp = new URLSearchParams(location.search).get('spotify');
        if (sp) emit({ type: 'spotify', info: demoSpotify(sp === 'tocando') });
      }
      if (msg.type === 'artistPhotos') {
        // No navegador de teste: a capa do primeiro álbum faz o papel da foto
        msg.artists.forEach((ar, k) => setTimeout(() => listeners.forEach((fn) => fn({ type: 'artistPhoto', name: ar.name, url: null })), 100 + k * 30));
      }
      if (msg.type === 'repair') listeners.forEach((fn) => fn({ type: 'repaired', id: msg.id, url: null }));
      if (msg.type === 'lyrics') {
        // Letra de mentira, só para ver o painel funcionando no navegador
        const versos = ['Luz de fim de tarde na janela', 'O disco gira devagar', '', 'Cada faixa conta uma história',
          'E a capa guarda o lugar', 'Volta o refrão, volta a lembrança', 'Ninguém precisa apressar', '',
          'Deixa tocar até o fim', 'Deixa o lado B girar'];
        const synced = versos.map((v, k) => { const t = 2 + k * 4; return `[${String(Math.floor(t / 60)).padStart(2, '0')}:${String(t % 60).padStart(2, '0')}.00] ${v}`; }).join('\n');
        setTimeout(() => listeners.forEach((fn) => fn({ type: 'lyrics', key: msg.key, synced, plain: null })), 400);
      }
    },
    on: (fn) => listeners.push(fn),
  };
}

export const host = window.chrome?.webview ? windowsHost() : demoHost();
