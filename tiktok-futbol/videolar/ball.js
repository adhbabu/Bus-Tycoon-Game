function classicBall(id, R = 100) {
  const d = Math.PI / 180, c = R;
  const at = (x, y, r, a) => [x + r * Math.cos(a), y + r * Math.sin(a)];
  const pent = (x, y, r, rot) => [0, 1, 2, 3, 4].map((j) => at(x, y, r, rot + j * 72 * d));
  const pts = (a) => a.map((p) => p.map((v) => v.toFixed(1)).join(',')).join(' ');
  const mid = pent(c, c, 0.3 * R, -90 * d);
  const Q = [0, 1, 2, 3, 4].map((i) => at(c, c, 0.56 * R, (-90 + 72 * i) * d));
  const outer = [0, 1, 2, 3, 4].map((i) => {
    const a = (-54 + 72 * i) * d;
    const [x, y] = at(c, c, 0.88 * R, a);
    return pent(x, y, 0.32 * R, a);
  });
  let ln = '';
  const L = (p, q) => { ln += `<line x1="${p[0].toFixed(1)}" y1="${p[1].toFixed(1)}" x2="${q[0].toFixed(1)}" y2="${q[1].toFixed(1)}"/>`; };
  mid.forEach((p, i) => L(p, Q[i]));
  const all = outer.flat();
  const dist = (u, v) => Math.hypot(u[0] - v[0], u[1] - v[1]);
  Q.forEach((q) => all.slice().sort((u, v) => dist(u, q) - dist(v, q)).slice(0, 2).forEach((v) => L(q, v)));
  outer.forEach((pg) => [pg[1], pg[4]].forEach((v) => {
    const a = Math.atan2(v[1] - c, v[0] - c);
    L(v, at(c, c, R * 1.2, a));
  }));
  return `<svg viewBox="0 0 ${2 * R} ${2 * R}" width="100%" height="100%">
    <defs>
      <clipPath id="${id}c"><circle cx="${c}" cy="${c}" r="${R - 1}"/></clipPath>
      <radialGradient id="${id}s" cx="36%" cy="30%" r="80%"><stop offset="0" stop-color="#fff" stop-opacity=".0"/><stop offset=".65" stop-color="#000" stop-opacity=".12"/><stop offset="1" stop-color="#000" stop-opacity=".5"/></radialGradient>
    </defs>
    <g clip-path="url(#${id}c)">
      <circle cx="${c}" cy="${c}" r="${R}" fill="#f7f7f2"/>
      <g stroke="#8d949c" stroke-width="${(R * 0.03).toFixed(1)}" stroke-linecap="round">${ln}</g>
      <polygon class="pent" points="${pts(mid)}" fill="#14161a"/>
      ${outer.map((pg) => `<polygon class="pent" points="${pts(pg)}" fill="#14161a"/>`).join('')}
      <circle cx="${c}" cy="${c}" r="${R}" fill="url(#${id}s)"/>
    </g>
    <circle cx="${c}" cy="${c}" r="${R - 1}" fill="none" stroke="#0b0d10" stroke-width="${(R * 0.03).toFixed(1)}"/>
  </svg>`;
}
