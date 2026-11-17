// Demo izleyicileri için profil resmi (gerçek yayında TikTok'un profil fotoğrafları kullanılır).
// Renkli degrade üstünde emoji yüz; dışarıdan resim indirmeden SVG olarak üretilir.
const FACES = ['😎', '🤩', '😄', '🥳', '😺', '🦊', '🐯', '🦁', '🐼', '🐸', '🙂', '😇', '🤠', '🐻', '🐨', '😜', '🤓', '🐵', '🦄', '🐶'];
const BGS = [
  ['#ff9a8b', '#ff6a88'], ['#a1c4fd', '#5b8def'], ['#fbc2eb', '#a6c1ee'], ['#84fab0', '#3ec7a4'],
  ['#ffd36e', '#fca35c'], ['#c3a6ff', '#7b61ff'], ['#7ee0ff', '#2fa5d6'], ['#ffb199', '#ff0844'],
];
export function fakeAvatar(i) {
  const [c1, c2] = BGS[i % BGS.length];
  const face = FACES[(i * 7 + 3) % FACES.length];
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">` +
    `<defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${c1}"/><stop offset="1" stop-color="${c2}"/></linearGradient></defs>` +
    `<rect width="100" height="100" fill="url(#g)"/>` +
    `<text x="50" y="57" font-size="60" text-anchor="middle" dominant-baseline="middle">${face}</text></svg>`;
  return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
}
