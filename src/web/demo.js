// Dados de exemplo, usados só quando a interface abre num navegador comum
// (para testar o visual). No Windows e no Alba a biblioteca vem dos seus arquivos.

const cover = (bg, fg, x, y) =>
  'data:image/svg+xml,' + encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="${bg}"/><circle cx="${x + 28}" cy="${y + 28}" r="28" fill="${fg}"/></svg>`);

const RAW = [
  ['A Day Without Rain', 'Enya', 2000, '#3E5C76', '#9DB4C0', 30, 18,
    ['A Day Without Rain', 'Wild Child', 'Only Time', 'Tempus Vernum', 'Deora Ar Mo Chroí', "Flora's Secret", 'Fallen Embers', 'Silver Inches', 'Pilgrim', 'One by One', 'The First of Autumn', 'Lazy Days']],
  ['Clube da Esquina', 'Milton Nascimento e Lô Borges', 1972, '#C8A165', '#6B4E2E', 8, 40, null],
  ['Currents', 'Tame Impala', 2015, '#5B2A86', '#E07A5F', 40, 40, null],
  ['In Rainbows', 'Radiohead', 2007, '#1F1F1F', '#E4572E', 22, 22, null],
  ['Random Access Memories', 'Daft Punk', 2013, '#2B2B2B', '#C9B37E', 22, 8, null],
  ['Acabou Chorare', 'Novos Baianos', 1972, '#E9C46A', '#2A9D8F', 36, 36, null],
  ['Blonde', 'Frank Ocean', 2016, '#7A8B6F', '#F1E3D3', 10, 12, null],
  ['Força Bruta', 'Jorge Ben', 1970, '#B23A48', '#F2D0A4', 44, 10, null],
  ['Rumours', 'Fleetwood Mac', 1977, '#D9CAB3', '#6D4C41', 22, 44, null],
  ['Kind of Blue', 'Miles Davis', 1959, '#1D3557', '#A8DADC', 6, 24, null],
];

export function demoLibrary() {
  const albums = RAW.map(([title, artist, year, bg, fg, x, y, names], i) => {
    const id = 'demo-' + i;
    const list = names ?? Array.from({ length: 8 }, (_, k) => `Faixa ${k + 1}`);
    return {
      id, title, artist, year, color: bg, added: 1000 - i,
      cover: cover(bg, fg, x, y),
      tracks: list.map((t, k) => ({
        id: `${id}-${k}`, title: t, artist, number: k + 1, disc: 1,
        duration: k === 2 && i === 0 ? 218 : 150 + ((k * 37 + i * 11) % 120),
        url: '',
      })),
    };
  });
  return { albums, folders: ['C:\\Users\\voce\\Music'] };
}

export function demoSpotify(playing) {
  return {
    title: 'Only Time', artist: 'Enya', album: 'A Day Without Rain',
    cover: cover('#3E5C76', '#9DB4C0', 30, 18), color: '#3E5C76',
    playing, position: 84, duration: 218, canSeek: true, canPrevious: true, canNext: true,
    source: 'Spotify', volume: 0.7,
  };
}
