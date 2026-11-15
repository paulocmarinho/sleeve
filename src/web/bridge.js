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
//   {type:'theme', dark}                  tema da interface, para a janela acompanhar
//   {type:'update', action:'check'|'restart'|'page'}
//
// Mensagens que a interface recebe:
//   {type:'library', albums:[…], folders:[…], scanning}
//   {type:'scan', count}
//   {type:'spotify', info:{…}|null}
//   {type:'settings', hiRes, startup, pinned, mini}
//   {type:'update', state:'idle'|'checking'|'downloading'|'ready'|'none'|'error', version, current}
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
        const sp = new URLSearchParams(location.search).get('spotify');
        if (sp) emit({ type: 'spotify', info: demoSpotify(sp === 'tocando') });
      }
    },
    on: (fn) => listeners.push(fn),
  };
}

export const host = window.chrome?.webview ? windowsHost() : demoHost();
