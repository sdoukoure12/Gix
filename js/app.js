/* ============================================================
   GIX – Main Application Script
   ============================================================ */

'use strict';

/* ── 0. Configuration Constants ── */
// TLE data uses epoch day 24020 = January 20 2024.
// Refresh TLE_DATA from https://celestrak.org/SOCRATES/ every ~2 weeks for accuracy.
const STARFIELD_COUNT          = 280;
const DEMO_NOTIFICATION_DELAY_MS = 30_000;
const DETAIL_MAP_INIT_DELAY_MS   = 120;  // wait for DOM visibility before Leaflet init

// TLE line-2 field indices (0-based after splitting on whitespace)
const TLE2_INCLINATION_IDX  = 2;
const TLE2_MEAN_MOTION_IDX  = 7;
const TLE2_DEFAULT_MEAN_MOTION = 15.5; // rev/day, typical LEO fallback

/* ── 1. State ── */
const STATE = {
  currentScreen: 'home',
  satellites: {},      // name -> { tle1, tle2, norad, type }
  positions: {},       // name -> { lat, lng, alt, speed, satrec }
  map: null,
  detailMap: null,
  detailMarker: null,
  mapMarkers: {},
  notifications: [],
  favoriteObjects: new Set(),
  skyObjects: [],
  arAnimFrame: null,
  selectedObject: null,
  arFilter: 'all',
  arStars: [],         // pre-generated starfield
  lastPositionUpdate: 0,
  updateInterval: null,
  mapTimeInterval: null,
};

/* ── 2. TLE Data ── */
// Real TLE data (epoch ~early 2024)
const TLE_DATA = {
  'ISS (ZARYA)': {
    tle1: '1 25544U 98067A   24020.54791667  .00016717  00000+0  30635-3 0  9994',
    tle2: '2 25544  51.6416 106.3110 0002691 323.2519 143.9988 15.49699478436827',
    norad: 25544,
    type: 'Station spatiale',
  },
  'HUBBLE': {
    tle1: '1 20580U 90037B   24020.54166667  .00000808  00000+0  39003-4 0  9996',
    tle2: '2 20580  28.4697 147.7962 0002530 111.8926 248.2302 15.09707837506390',
    norad: 20580,
    type: 'Télescope spatial',
  },
  'STARLINK-1007': {
    tle1: '1 44713U 19074B   24020.50000000  .00001020  00000+0  72315-4 0  9991',
    tle2: '2 44713  53.0535  78.4321 0001420  88.9231 271.2145 15.06421318228463',
    norad: 44713,
    type: 'Megaconstellation',
  },
  'STARLINK-1008': {
    tle1: '1 44714U 19074C   24020.50000000  .00001015  00000+0  71841-4 0  9997',
    tle2: '2 44714  53.0540  78.4267 0001380  89.1156 271.0012 15.06422512228471',
    norad: 44714,
    type: 'Megaconstellation',
  },
  'NOAA 19': {
    tle1: '1 33591U 09005A   24020.54791667  .00000077  00000+0  63849-4 0  9993',
    tle2: '2 33591  99.1888 335.6452 0013977 215.1284 144.8712 14.12295684777982',
    norad: 33591,
    type: 'Météo',
  },
  'TERRA': {
    tle1: '1 25994U 99068A   24020.54166667  .00000018  00000+0  20476-4 0  9991',
    tle2: '2 25994  98.2036  10.4268 0001286  90.4523 269.6791 14.57126123289764',
    norad: 25994,
    type: 'Observation Terre',
  },
  'AQUA': {
    tle1: '1 27424U 02022A   24020.54166667  .00000042  00000+0  27634-4 0  9994',
    tle2: '2 27424  98.2106  10.5827 0001543 124.7912 235.3412 14.57122684162543',
    norad: 27424,
    type: 'Observation Terre',
  },
  'TIANGONG': {
    tle1: '1 48274U 21035A   24020.54791667  .00015833  00000+0  19476-3 0  9990',
    tle2: '2 48274  41.4698 140.0193 0006026  91.2715 268.8828 15.61340798150498',
    norad: 48274,
    type: 'Station spatiale',
  },
};

/* ── 3. Named Stars for AR ── */
const NAMED_STARS = [
  { name: 'Sirius',     ra: 101.29, dec: -16.72, mag: -1.46, color: '#B0D8FF' },
  { name: 'Canopus',    ra: 95.99,  dec: -52.70, mag: -0.72, color: '#FFFFE0' },
  { name: 'Arcturus',   ra: 213.92, dec: 19.18,  mag: -0.05, color: '#FFB347' },
  { name: 'Vega',       ra: 279.23, dec: 38.78,  mag:  0.03, color: '#CCE5FF' },
  { name: 'Capella',    ra: 79.17,  dec: 45.99,  mag:  0.08, color: '#FFE080' },
  { name: 'Rigel',      ra: 78.63,  dec: -8.20,  mag:  0.13, color: '#B0C4FF' },
  { name: 'Procyon',    ra: 114.83, dec:  5.23,  mag:  0.38, color: '#FFFACD' },
  { name: 'Betelgeuse', ra: 88.79,  dec:  7.41,  mag:  0.42, color: '#FF6B47' },
  { name: 'Polaris',    ra: 37.95,  dec: 89.26,  mag:  1.97, color: '#F0F8FF' },
  { name: 'Aldebaran',  ra: 68.98,  dec: 16.51,  mag:  0.87, color: '#FF7F50' },
  { name: 'Spica',      ra: 201.30, dec: -11.16, mag:  0.98, color: '#9BB8FF' },
  { name: 'Antares',    ra: 247.35, dec: -26.43, mag:  1.06, color: '#FF4500' },
  { name: 'Fomalhaut',  ra: 344.41, dec: -29.62, mag:  1.16, color: '#EEF0FF' },
  { name: 'Deneb',      ra: 310.36, dec: 45.28,  mag:  1.25, color: '#DDE8FF' },
  { name: 'Regulus',    ra: 152.09, dec: 11.97,  mag:  1.36, color: '#CCE5FF' },
  { name: 'Castor',     ra: 113.65, dec: 31.89,  mag:  1.58, color: '#E8EFFF' },
  { name: 'Pollux',     ra: 116.33, dec: 28.03,  mag:  1.14, color: '#FFD27F' },
];

/* ── 4. Constellation Line Segments (RA/Dec pairs) ── */
const CONSTELLATIONS = {
  Orion: [
    // Belt
    [{ ra: 84.05,  dec:  -1.20 }, { ra: 83.86,  dec:  -1.94 }],
    [{ ra: 83.86,  dec:  -1.94 }, { ra: 82.06,  dec:  -2.60 }],
    // Shoulders
    [{ ra: 88.79,  dec:   7.41 }, { ra: 82.06,  dec:  -2.60 }],
    [{ ra: 78.63,  dec:  -8.20 }, { ra: 84.05,  dec:  -1.20 }],
    // Feet
    [{ ra: 88.79,  dec:   7.41 }, { ra: 84.05,  dec:  -1.20 }],
    [{ ra: 78.63,  dec:  -8.20 }, { ra: 82.06,  dec:  -2.60 }],
  ],
  BigDipper: [
    [{ ra: 165.93, dec: 61.75 }, { ra: 165.46, dec: 56.38 }],
    [{ ra: 165.46, dec: 56.38 }, { ra: 178.46, dec: 53.69 }],
    [{ ra: 178.46, dec: 53.69 }, { ra: 193.51, dec: 55.96 }],
    [{ ra: 193.51, dec: 55.96 }, { ra: 206.88, dec: 49.31 }],
    [{ ra: 206.88, dec: 49.31 }, { ra: 200.98, dec: 54.92 }],
    [{ ra: 200.98, dec: 54.92 }, { ra: 193.51, dec: 55.96 }],
  ],
};

/* ── 5. Upcoming Events ── */
const UPCOMING_EVENTS = [
  { name: 'Éclipse de Lune',           time: '14 Mars à 02h45',  color: 'danger',    icon: 'fa-moon' },
  { name: 'Conjonction Lune–Jupiter',  time: 'Ce soir à 22h00',  color: 'secondary', icon: 'fa-circle-dot' },
  { name: 'Pluie de météores Lyrids',  time: '22–23 Avril',      color: 'primary',   icon: 'fa-meteor' },
  { name: 'Passage ISS visible',        time: 'Dans ~18 min',     color: 'success',   icon: 'fa-satellite' },
];

/* ── 6. Notifications Data ── */
const NOTIFICATIONS_DATA = [
  {
    id: 1,
    icon: 'fa-satellite',
    iconClass: 'iss',
    title: 'ISS visible dans 10 min – direction Sud-Ouest',
    time: 'Il y a 2 minutes',
    read: false,
  },
  {
    id: 2,
    icon: 'fa-moon',
    iconClass: 'luna',
    title: 'Conjonction Lune–Jupiter ce soir à 22h',
    time: 'Il y a 1 heure',
    read: false,
  },
  {
    id: 3,
    icon: 'fa-satellite-dish',
    iconClass: 'starlink',
    title: 'Nouveau lot de satellites Starlink détecté',
    time: 'Il y a 3 heures',
    read: false,
  },
  {
    id: 4,
    icon: 'fa-eye',
    iconClass: 'hubble',
    title: 'Passage Hubble dans 2h30 – direction Nord-Est',
    time: 'Il y a 5 heures',
    read: true,
  },
  {
    id: 5,
    icon: 'fa-circle-half-stroke',
    iconClass: 'eclipse',
    title: 'Éclipse de Lune le 14 mars à 02h45',
    time: 'Hier',
    read: true,
  },
];

/* ════════════════════════════════════════════════
   INITIALIZATION
════════════════════════════════════════════════ */
document.addEventListener('DOMContentLoaded', () => {
  initNavigation();
  initSatellites();
  initHome();
  initAR();
  initMap();
  initNotifications();
  renderFavorites();
  requestNotificationPermission();

  // Schedule demo browser notification
  setTimeout(fireDemoNotification, DEMO_NOTIFICATION_DELAY_MS);

  // Start position update loop
  updateAllPositions();
  STATE.updateInterval = setInterval(updateAllPositions, 3000);
});

/* ════════════════════════════════════════════════
   NAVIGATION
════════════════════════════════════════════════ */
function initNavigation() {
  // Bottom nav buttons
  document.querySelectorAll('.nav-item[data-screen]').forEach(btn => {
    btn.addEventListener('click', () => {
      const screen = btn.dataset.screen;
      showScreen(screen);
    });
  });

  // Back buttons
  document.getElementById('ar-back-btn').addEventListener('click', () => showScreen('home'));
  document.getElementById('detail-back-btn').addEventListener('click', () => {
    showScreen(STATE.previousScreen || 'home');
  });

  // AR launch
  document.getElementById('btn-open-ar').addEventListener('click', () => showScreen('ar'));

  // Detail → map
  document.getElementById('btn-detail-to-map').addEventListener('click', () => showScreen('map'));

  // Fav toggle
  document.getElementById('btn-fav-toggle').addEventListener('click', toggleFavorite);

  // ISS view toggle
  document.getElementById('btn-iss-view').addEventListener('click', toggleISSView);
}

function showScreen(name) {
  const prev = STATE.currentScreen;

  // Hide previous screen
  const prevEl = document.getElementById('screen-' + prev);
  if (prevEl) prevEl.classList.remove('active');

  // Update nav
  document.querySelectorAll('.nav-item[data-screen]').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.screen === name);
  });

  // Show new screen
  const nextEl = document.getElementById('screen-' + name);
  if (!nextEl) return;
  nextEl.classList.add('active');

  STATE.previousScreen = prev;
  STATE.currentScreen = name;

  // Screen-specific init/cleanup
  if (name === 'ar') {
    startARLoop();
  } else {
    stopARLoop();
  }

  if (name === 'map') {
    initMapIfNeeded();
    startMapUpdates();
  } else {
    stopMapUpdates();
  }

  if (name === 'detail' && STATE.selectedObject) {
    refreshDetailScreen(STATE.selectedObject);
  }

  if (name === 'notifications') {
    renderNotifications();
  }

  if (name === 'favorites') {
    renderFavorites();
  }
}

/* ════════════════════════════════════════════════
   SATELLITE INIT & PROPAGATION
════════════════════════════════════════════════ */
function initSatellites() {
  Object.entries(TLE_DATA).forEach(([name, data]) => {
    try {
      const satrec = satellite.twoline2satrec(data.tle1, data.tle2);
      STATE.satellites[name] = { ...data, satrec };
    } catch (e) {
      console.warn('[Gix] TLE parse error for', name, e);
    }
  });
}

function propagateSatellite(name) {
  const sat = STATE.satellites[name];
  if (!sat || !sat.satrec) return null;

  try {
    const now = new Date();
    const posVel = satellite.propagate(sat.satrec, now);
    if (!posVel || !posVel.position) return null;

    const gmst = satellite.gstime(now);
    const geo  = satellite.eciToGeodetic(posVel.position, gmst);

    const lat  = satellite.degreesLat(geo.latitude);
    const lng  = satellite.degreesLong(geo.longitude);
    const alt  = geo.height;                       // km

    // Speed from velocity vector (km/s → km/h)
    const vel  = posVel.velocity;
    const speed = Math.sqrt(vel.x**2 + vel.y**2 + vel.z**2) * 3600;

    return { lat, lng, alt, speed };
  } catch (e) {
    return null;
  }
}

function updateAllPositions() {
  Object.keys(STATE.satellites).forEach(name => {
    const pos = propagateSatellite(name);
    if (pos) STATE.positions[name] = pos;
  });

  // Refresh live data displays
  refreshHomeWidgets();
  if (STATE.currentScreen === 'detail' && STATE.selectedObject) {
    refreshDetailLiveData(STATE.selectedObject);
  }
  if (STATE.currentScreen === 'map') {
    updateMapMarkers();
  }

  STATE.lastPositionUpdate = Date.now();
}

/* ════════════════════════════════════════════════
   HOME SCREEN
════════════════════════════════════════════════ */
function initHome() {
  // Clock
  updateClock();
  setInterval(updateClock, 1000);

  // Events
  renderEvents();

  // Satellite quick list
  renderSatQuickList();
}

function updateClock() {
  const now  = new Date();
  const time = now.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  const date = now.toLocaleDateString('fr-FR', { weekday: 'long', day: 'numeric', month: 'long' });
  document.getElementById('clock-time').textContent = time;
  document.getElementById('clock-date').textContent = date.charAt(0).toUpperCase() + date.slice(1);
}

function refreshHomeWidgets() {
  const issPos = STATE.positions['ISS (ZARYA)'];
  if (!issPos) return;

  // Compute approximate next visible pass (simplified: based on current orbit period)
  const nextPassMin = computeNextPassMin(issPos);
  document.getElementById('iss-next-pass').textContent = `Dans ~${nextPassMin} min`;
  document.getElementById('iss-direction').textContent  = `Direction : ${issPos.lat > 0 ? 'Nord' : 'Sud'}-${issPos.lng > 0 ? 'Est' : 'Ouest'}`;
}

function computeNextPassMin(issPos) {
  // Simplified demo estimate only — not a real orbital visibility calculation.
  // A true pass prediction requires observer lat/lng, elevation angle, and
  // darkness conditions. Use a service like Heavens-Above for real predictions.
  const periodMin = 92; // ISS orbital period ≈ 92 min
  const offset = Math.abs(issPos.lat) / 51.6 * 46;
  return Math.round(10 + (offset % (periodMin / 2)));
}

function renderEvents() {
  const container = document.getElementById('events-list');
  container.innerHTML = UPCOMING_EVENTS.map((ev, i) => `
    <div class="event-card" style="animation-delay:${i * 0.06}s">
      <div class="event-dot ${ev.color}"></div>
      <div class="event-info">
        <div class="event-name">${ev.name}</div>
        <div class="event-time"><i class="fa-solid fa-clock"></i> ${ev.time}</div>
      </div>
      <i class="fa-solid fa-chevron-right event-arrow"></i>
    </div>
  `).join('');
}

function renderSatQuickList() {
  const container = document.getElementById('sat-quick-list');
  const names = Object.keys(TLE_DATA);
  container.innerHTML = names.map((name, i) => {
    const data = TLE_DATA[name];
    return `
      <div class="event-card" style="animation-delay:${i * 0.04}s" data-sat="${name}" role="button" tabindex="0">
        <i class="fa-solid fa-satellite" style="color:var(--primary);font-size:16px;flex-shrink:0;"></i>
        <div class="event-info">
          <div class="event-name">${name}</div>
          <div class="event-time">${data.type} · NORAD ${data.norad}</div>
        </div>
        <i class="fa-solid fa-chevron-right event-arrow"></i>
      </div>
    `;
  }).join('');

  container.querySelectorAll('[data-sat]').forEach(el => {
    el.addEventListener('click', () => openDetailScreen(el.dataset.sat));
    el.addEventListener('keydown', e => { if (e.key === 'Enter') openDetailScreen(el.dataset.sat); });
  });
}

/* ════════════════════════════════════════════════
   AR SCREEN
════════════════════════════════════════════════ */
function initAR() {
  const canvas = document.getElementById('ar-canvas');
  generateStarfield();

  // Filter buttons
  document.querySelectorAll('.ar-filter-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.ar-filter-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      STATE.arFilter = btn.dataset.filter;
    });
  });

  // Click detection on canvas
  canvas.addEventListener('click', handleARClick);
  canvas.addEventListener('touchend', e => {
    e.preventDefault();
    const touch = e.changedTouches[0];
    handleARClick({ clientX: touch.clientX, clientY: touch.clientY });
  }, { passive: false });
}

function generateStarfield() {
  STATE.arStars = [];
  for (let i = 0; i < STARFIELD_COUNT; i++) {
    STATE.arStars.push({
      x: Math.random(),
      y: Math.random(),
      r: Math.random() * 1.6 + 0.3,
      baseAlpha: Math.random() * 0.5 + 0.35,
      twinkleSpeed: Math.random() * 0.018 + 0.005,
      twinkleOffset: Math.random() * Math.PI * 2,
      color: pickStarColor(),
    });
  }
}

function pickStarColor() {
  const colors = ['#FFFFFF', '#CCE5FF', '#FFE8CC', '#FFD0C0', '#E8F4FF'];
  return colors[Math.floor(Math.random() * colors.length)];
}

function startARLoop() {
  if (STATE.arAnimFrame) return;
  const canvas = document.getElementById('ar-canvas');
  const parent = canvas.parentElement;

  function resize() {
    canvas.width  = parent.clientWidth;
    canvas.height = parent.clientHeight;
  }
  resize();
  window.addEventListener('resize', resize);

  let t = 0;
  function loop() {
    t += 0.016;
    drawARFrame(canvas, t);
    STATE.arAnimFrame = requestAnimationFrame(loop);
  }
  STATE.arAnimFrame = requestAnimationFrame(loop);
}

function stopARLoop() {
  if (STATE.arAnimFrame) {
    cancelAnimationFrame(STATE.arAnimFrame);
    STATE.arAnimFrame = null;
  }
}

function drawARFrame(canvas, t) {
  const ctx = canvas.getContext('2d');
  const W = canvas.width;
  const H = canvas.height;

  // Background gradient
  const grad = ctx.createLinearGradient(0, 0, 0, H);
  grad.addColorStop(0,   '#000308');
  grad.addColorStop(0.5, '#010918');
  grad.addColorStop(1,   '#020614');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, W, H);

  // Nebula glow patches
  drawNebula(ctx, W * 0.7, H * 0.3, 150, 'rgba(79,195,247,0.04)');
  drawNebula(ctx, W * 0.25, H * 0.6, 120, 'rgba(124,77,255,0.04)');

  const filter = STATE.arFilter;

  // Constellation lines
  if (filter === 'all' || filter === 'constellations') {
    drawConstellations(ctx, W, H, t);
  }

  // Stars
  if (filter === 'all' || filter === 'stars') {
    drawStarfield(ctx, W, H, t);
    drawNamedStars(ctx, W, H, t);
  }

  // Satellites
  if (filter === 'all' || filter === 'satellites') {
    drawSatellites(ctx, W, H, t);
  }

  // Horizon line
  drawHorizon(ctx, W, H);
}

function drawNebula(ctx, cx, cy, r, color) {
  const g = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
  g.addColorStop(0, color);
  g.addColorStop(1, 'transparent');
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.arc(cx, cy, r, 0, Math.PI * 2);
  ctx.fill();
}

function drawConstellations(ctx, W, H, t) {
  ctx.save();
  ctx.strokeStyle = 'rgba(79,195,247,0.18)';
  ctx.lineWidth = 0.8;
  ctx.setLineDash([4, 8]);

  Object.values(CONSTELLATIONS).forEach(lines => {
    lines.forEach(([a, b]) => {
      const ax = raDecToCanvasX(a.ra, W);
      const ay = raDecToCanvasY(a.dec, H);
      const bx = raDecToCanvasX(b.ra, W);
      const by = raDecToCanvasY(b.dec, H);
      ctx.beginPath();
      ctx.moveTo(ax, ay);
      ctx.lineTo(bx, by);
      ctx.stroke();
    });
  });

  ctx.restore();
}

function drawStarfield(ctx, W, H, t) {
  STATE.arStars.forEach(star => {
    const alpha = star.baseAlpha + Math.sin(t * star.twinkleSpeed * 60 + star.twinkleOffset) * 0.25;
    const px = star.x * W;
    const py = star.y * H;

    ctx.beginPath();
    ctx.arc(px, py, star.r, 0, Math.PI * 2);
    ctx.fillStyle = hexToRgba(star.color, Math.max(0.05, Math.min(1, alpha)));
    ctx.fill();
  });
}

function drawNamedStars(ctx, W, H, t) {
  NAMED_STARS.forEach(star => {
    const x = raDecToCanvasX(star.ra, W);
    const y = raDecToCanvasY(star.dec, H);
    const r = Math.max(2, 4 - star.mag * 1.2);
    const pulse = 1 + Math.sin(t * 1.2 + star.ra) * 0.12;

    // Glow
    const glow = ctx.createRadialGradient(x, y, 0, x, y, r * 3 * pulse);
    glow.addColorStop(0, hexToRgba(star.color, 0.45));
    glow.addColorStop(1, 'transparent');
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(x, y, r * 3 * pulse, 0, Math.PI * 2);
    ctx.fill();

    // Core
    ctx.beginPath();
    ctx.arc(x, y, r * pulse, 0, Math.PI * 2);
    ctx.fillStyle = star.color;
    ctx.fill();

    // Label (brighter stars)
    if (star.mag < 1.5 && W > 200) {
      ctx.font = '10px Inter, sans-serif';
      ctx.fillStyle = 'rgba(255,255,255,0.6)';
      ctx.fillText(star.name, x + r + 4, y + 3);
    }
  });
}

function drawSatellites(ctx, W, H, t) {
  Object.entries(STATE.positions).forEach(([name, pos]) => {
    // Map lat/lng to a simulated sky position
    const x = ((pos.lng + 180) / 360) * W;
    const y = ((90 - pos.lat) / 180) * H;

    const isISS = name.includes('ISS');
    const isTiangong = name.includes('TIANGONG');

    const baseColor = isISS ? '#64FFDA' : isTiangong ? '#FFD740' : '#4FC3F7';
    const pulse = 1 + Math.sin(t * 3 + pos.lat) * 0.2;

    // Trail
    ctx.save();
    ctx.strokeStyle = hexToRgba(baseColor, 0.25);
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(x - 18, y);
    ctx.lineTo(x, y);
    ctx.stroke();
    ctx.restore();

    // Glow ring
    const glow = ctx.createRadialGradient(x, y, 0, x, y, 14 * pulse);
    glow.addColorStop(0, hexToRgba(baseColor, 0.3));
    glow.addColorStop(1, 'transparent');
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(x, y, 14 * pulse, 0, Math.PI * 2);
    ctx.fill();

    // Core dot
    ctx.beginPath();
    ctx.arc(x, y, 3.5, 0, Math.PI * 2);
    ctx.fillStyle = baseColor;
    ctx.fill();

    // Diamond shape for ISS
    if (isISS) {
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(Math.PI / 4);
      ctx.strokeStyle = baseColor;
      ctx.lineWidth = 1.5;
      ctx.strokeRect(-5, -5, 10, 10);
      ctx.restore();
    }

    // Label
    ctx.font = `bold 10px Inter, sans-serif`;
    ctx.fillStyle = hexToRgba(baseColor, 0.9);
    ctx.fillText(name.split('(')[0].trim().substring(0, 12), x + 7, y - 6);

    // Alt badge
    if (pos.alt) {
      ctx.font = '9px Inter, sans-serif';
      ctx.fillStyle = 'rgba(255,255,255,0.4)';
      ctx.fillText(`${Math.round(pos.alt)} km`, x + 7, y + 5);
    }
  });
}

function drawHorizon(ctx, W, H) {
  ctx.save();
  ctx.strokeStyle = 'rgba(79,195,247,0.12)';
  ctx.lineWidth = 1;
  ctx.setLineDash([6, 12]);
  ctx.beginPath();
  ctx.moveTo(0, H * 0.75);
  ctx.lineTo(W, H * 0.75);
  ctx.stroke();

  ctx.font = '10px Inter, sans-serif';
  ctx.fillStyle = 'rgba(79,195,247,0.4)';
  ctx.fillText('Horizon', 8, H * 0.75 - 5);
  ctx.restore();
}

function handleARClick(e) {
  const canvas = document.getElementById('ar-canvas');
  const rect   = canvas.getBoundingClientRect();
  const cx = e.clientX - rect.left;
  const cy = e.clientY - rect.top;
  const W  = canvas.width;
  const H  = canvas.height;

  // Check satellite hit
  for (const [name, pos] of Object.entries(STATE.positions)) {
    const sx = ((pos.lng + 180) / 360) * W;
    const sy = ((90 - pos.lat) / 180) * H;
    const dist = Math.hypot(cx - sx, cy - sy);
    if (dist < 22) {
      showARToast(`Sélection : ${name}`);
      setTimeout(() => openDetailScreen(name), 400);
      return;
    }
  }

  // Check named star hit
  for (const star of NAMED_STARS) {
    const sx = raDecToCanvasX(star.ra, W);
    const sy = raDecToCanvasY(star.dec, H);
    const dist = Math.hypot(cx - sx, cy - sy);
    if (dist < 16) {
      showARToast(`${star.name}  ·  mag ${star.mag.toFixed(2)}`);
      return;
    }
  }
}

function showARToast(msg) {
  const toast = document.getElementById('ar-toast');
  toast.textContent = msg;
  toast.classList.add('visible');
  setTimeout(() => toast.classList.remove('visible'), 2500);
}

/* AR helpers */
function raDecToCanvasX(ra, W) {
  return (ra / 360) * W;
}
function raDecToCanvasY(dec, H) {
  return ((90 - dec) / 180) * H;
}
function hexToRgba(hex, a) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r},${g},${b},${a})`;
}

/* ════════════════════════════════════════════════
   DETAIL SCREEN
════════════════════════════════════════════════ */
function openDetailScreen(satName) {
  STATE.selectedObject = satName;
  document.getElementById('iss-view-panel').classList.remove('visible');
  showScreen('detail');
}

function refreshDetailScreen(name) {
  const data = TLE_DATA[name];
  if (!data) return;

  document.getElementById('detail-name').textContent  = name;
  document.getElementById('detail-type').textContent  = data.type;
  document.getElementById('detail-norad').textContent = `NORAD ID: ${data.norad}`;

  // Inclination & period from TLE line 2
  // Note: field parsing is format-dependent; indices match standard TLE spacing.
  const tle2Parts = data.tle2.split(/\s+/);
  const inc    = parseFloat(tle2Parts[TLE2_INCLINATION_IDX] || 0).toFixed(2);
  const mmDeg  = parseFloat(tle2Parts[TLE2_MEAN_MOTION_IDX] || TLE2_DEFAULT_MEAN_MOTION);
  const period = (1440 / mmDeg).toFixed(1);

  document.getElementById('detail-inc').textContent    = inc;
  document.getElementById('detail-period').textContent = period;

  // Fav button
  const favBtn = document.getElementById('btn-fav-toggle');
  if (STATE.favoriteObjects.has(name)) {
    favBtn.classList.add('active');
    favBtn.innerHTML = '<i class="fa-solid fa-star"></i> Dans vos favoris';
  } else {
    favBtn.classList.remove('active');
    favBtn.innerHTML = '<i class="fa-solid fa-star"></i> Ajouter aux favoris';
  }

  // ISS view button visibility
  const issViewBtn = document.getElementById('btn-iss-view');
  issViewBtn.style.display = (name === 'ISS (ZARYA)' || name === 'TIANGONG') ? 'flex' : 'none';

  refreshDetailLiveData(name);
  initDetailMap(name);
}

function refreshDetailLiveData(name) {
  const pos = STATE.positions[name];
  if (!pos) return;

  document.getElementById('detail-alt').textContent   = Math.round(pos.alt);
  document.getElementById('detail-speed').textContent = Math.round(pos.speed).toLocaleString('fr-FR');
  document.getElementById('detail-lat').textContent   = pos.lat.toFixed(3);
  document.getElementById('detail-lng').textContent   = pos.lng.toFixed(3);

  // Update detail map marker
  if (STATE.detailMap && STATE.detailMarker) {
    STATE.detailMarker.setLatLng([pos.lat, pos.lng]);
    STATE.detailMap.panTo([pos.lat, pos.lng]);
  }
}

function initDetailMap(name) {
  const pos = STATE.positions[name] || { lat: 0, lng: 0 };

  if (STATE.detailMap) {
    STATE.detailMap.remove();
    STATE.detailMap = null;
    STATE.detailMarker = null;
  }

  const container = document.getElementById('detail-map');
  // Delay init by DETAIL_MAP_INIT_DELAY_MS to ensure the container has
  // non-zero dimensions after the screen transition completes.
  setTimeout(() => {
    try {
      const map = L.map(container, { zoomControl: false, attributionControl: false });
      L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
        subdomains: 'abcd',
        maxZoom: 10,
      }).addTo(map);

      map.setView([pos.lat, pos.lng], 2);

      const satIcon = L.divIcon({
        html: `<div style="
          width:14px;height:14px;border-radius:50%;
          background:#64FFDA;box-shadow:0 0 10px #64FFDA;
          border:2px solid white;"></div>`,
        iconSize: [14, 14],
        iconAnchor: [7, 7],
        className: '',
      });

      const marker = L.marker([pos.lat, pos.lng], { icon: satIcon }).addTo(map);
      STATE.detailMap = map;
      STATE.detailMarker = marker;
    } catch (e) {
      console.warn('[Gix] Detail map init error', e);
    }
  }, DETAIL_MAP_INIT_DELAY_MS);
}

function toggleFavorite() {
  const name = STATE.selectedObject;
  if (!name) return;

  if (STATE.favoriteObjects.has(name)) {
    STATE.favoriteObjects.delete(name);
  } else {
    STATE.favoriteObjects.add(name);
  }

  // Refresh button state
  refreshDetailScreen(name);

  // Update profile stat
  document.getElementById('stat-favs').textContent = STATE.favoriteObjects.size;
}

function toggleISSView() {
  const panel = document.getElementById('iss-view-panel');
  panel.classList.toggle('visible');

  const name = STATE.selectedObject;
  const pos  = STATE.positions[name];
  if (pos && panel.classList.contains('visible')) {
    document.getElementById('iss-view-desc').textContent =
      `À une altitude de ${Math.round(pos.alt)} km, ${name} survole actuellement ` +
      `la région à ${pos.lat.toFixed(1)}°${pos.lat > 0 ? 'N' : 'S'} / ` +
      `${pos.lng.toFixed(1)}°${pos.lng > 0 ? 'E' : 'O'}. ` +
      `La vue en direct est disponible via le flux NASA HDEV. ` +
      `À cette vitesse de ${Math.round(pos.speed).toLocaleString('fr-FR')} km/h, ` +
      `l'équipage voit le lever du Soleil 16 fois par jour.`;
  }
}

/* ════════════════════════════════════════════════
   MAP SCREEN
════════════════════════════════════════════════ */
function initMapIfNeeded() {
  if (STATE.map) return;

  try {
    const map = L.map('satellite-map', {
      zoomControl: true,
      attributionControl: true,
      worldCopyJump: true,
    });

    L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
      attribution: '© <a href="https://carto.com">CARTO</a>',
      subdomains: 'abcd',
      maxZoom: 18,
    }).addTo(map);

    map.setView([20, 0], 2);

    STATE.map = map;
    updateMapMarkers();
  } catch (e) {
    console.warn('[Gix] Map init error', e);
  }
}

function updateMapMarkers() {
  if (!STATE.map) return;

  Object.entries(STATE.positions).forEach(([name, pos]) => {
    const isISS = name.includes('ISS');
    const isTiangong = name.includes('TIANGONG');
    const color = isISS ? '#64FFDA' : isTiangong ? '#FFD740' : '#4FC3F7';
    const size  = isISS ? 14 : 10;

    const iconHtml = `<div style="
      width:${size}px;height:${size}px;border-radius:50%;
      background:${color};box-shadow:0 0 ${size}px ${color};
      border:2px solid rgba(255,255,255,0.6);
      cursor:pointer;"></div>`;

    const icon = L.divIcon({ html: iconHtml, iconSize: [size, size], iconAnchor: [size/2, size/2], className: '' });

    if (STATE.mapMarkers[name]) {
      STATE.mapMarkers[name].setLatLng([pos.lat, pos.lng]);
    } else {
      const marker = L.marker([pos.lat, pos.lng], { icon })
        .addTo(STATE.map)
        .bindTooltip(`<b style="color:${color}">${name}</b><br>Alt: ${Math.round(pos.alt)} km`, {
          permanent: false,
          direction: 'top',
          className: 'gix-tooltip',
          opacity: 0.92,
        })
        .on('click', () => openDetailScreen(name));

      STATE.mapMarkers[name] = marker;
    }
  });

  // Update time display
  const now = new Date();
  const el  = document.getElementById('map-time-display');
  if (el) {
    el.textContent = `Maintenant · ${now.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}`;
  }
}

function startMapUpdates() {
  stopMapUpdates();
  STATE.mapTimeInterval = setInterval(() => {
    updateMapMarkers();
  }, 3000);
}

function stopMapUpdates() {
  if (STATE.mapTimeInterval) {
    clearInterval(STATE.mapTimeInterval);
    STATE.mapTimeInterval = null;
  }
}

function initMap() {
  // Map is lazy-initialized on screen show
}

/* ════════════════════════════════════════════════
   NOTIFICATIONS SCREEN
════════════════════════════════════════════════ */
function initNotifications() {
  STATE.notifications = [...NOTIFICATIONS_DATA];
}

function renderNotifications() {
  const list = document.getElementById('notif-list');
  const badge = document.getElementById('notif-badge');
  const unread = STATE.notifications.filter(n => !n.read).length;

  badge.textContent = unread;
  badge.style.display = unread > 0 ? 'flex' : 'none';

  list.innerHTML = STATE.notifications.map((n, i) => `
    <div class="notif-item ${n.read ? 'read' : ''}" data-id="${n.id}"
         style="animation-delay:${i * 0.07}s">
      <div class="notif-icon-wrap ${n.iconClass}">
        <i class="fa-solid ${n.icon}"></i>
      </div>
      <div class="notif-body">
        <div class="notif-title">${n.title}</div>
        <div class="notif-time">${n.time}</div>
      </div>
      <button class="notif-dismiss" data-id="${n.id}" aria-label="Ignorer">
        <i class="fa-solid fa-xmark"></i>
      </button>
    </div>
  `).join('');

  list.querySelectorAll('.notif-dismiss').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      dismissNotification(parseInt(btn.dataset.id));
    });
  });

  list.querySelectorAll('.notif-item').forEach(item => {
    item.addEventListener('click', () => {
      markNotificationRead(parseInt(item.dataset.id));
    });
  });
}

function dismissNotification(id) {
  STATE.notifications = STATE.notifications.filter(n => n.id !== id);
  renderNotifications();
}

function markNotificationRead(id) {
  const notification = STATE.notifications.find(n => n.id === id);
  if (notification) {
    notification.read = true;
    renderNotifications();
  }
}

/* ════════════════════════════════════════════════
   FAVORITES SCREEN
════════════════════════════════════════════════ */
function renderFavorites() {
  const list = document.getElementById('favs-list');
  const favs = Array.from(STATE.favoriteObjects);

  if (favs.length === 0) {
    list.innerHTML = `
      <div class="favs-empty">
        <i class="fa-regular fa-star"></i>
        <p>Aucun favori pour l'instant.<br>Ajoutez des satellites depuis leur page de détail.</p>
      </div>`;
    return;
  }

  list.innerHTML = favs.map(name => {
    const data = TLE_DATA[name];
    const pos  = STATE.positions[name];
    return `
      <div class="fav-item" data-sat="${name}" role="button" tabindex="0">
        <div class="fav-icon"><i class="fa-solid fa-satellite"></i></div>
        <div class="fav-info">
          <div class="fav-name">${name}</div>
          <div class="fav-sub">${data ? data.type : '—'} ${pos ? `· ${Math.round(pos.alt)} km` : ''}</div>
        </div>
        <i class="fa-solid fa-chevron-right fav-chevron"></i>
      </div>`;
  }).join('');

  list.querySelectorAll('[data-sat]').forEach(el => {
    el.addEventListener('click', () => openDetailScreen(el.dataset.sat));
    el.addEventListener('keydown', e => { if (e.key === 'Enter') openDetailScreen(el.dataset.sat); });
  });
}

/* ════════════════════════════════════════════════
   BROWSER NOTIFICATIONS (Web API)
════════════════════════════════════════════════ */
function requestNotificationPermission() {
  if (!('Notification' in window)) return;
  if (Notification.permission === 'default') {
    Notification.requestPermission().catch(() => {});
  }
}

function fireDemoNotification() {
  if (!('Notification' in window)) return;
  if (Notification.permission !== 'granted') return;

  try {
    const issPos = STATE.positions['ISS (ZARYA)'];
    const body   = issPos
      ? `L'ISS est à ${Math.round(issPos.alt)} km d'altitude · ${Math.round(issPos.speed).toLocaleString('fr-FR')} km/h`
      : 'Ouvrez Gix pour suivre la Station Spatiale Internationale.';

    new Notification('🛸 Gix – ISS visible bientôt !', {
      body,
      icon: 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><circle cx="16" cy="16" r="16" fill="%230A0F1E"/><text y="22" x="7" font-size="18">🛰</text></svg>',
      badge: 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><circle cx="16" cy="16" r="16" fill="%234FC3F7"/></svg>',
    });
  } catch (e) {
    // Notification may fail silently
  }
}

/* ════════════════════════════════════════════════
   CLEANUP ON PAGE HIDE
════════════════════════════════════════════════ */
document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    stopARLoop();
    stopMapUpdates();
    clearInterval(STATE.updateInterval);
  } else {
    if (STATE.currentScreen === 'ar') startARLoop();
    if (STATE.currentScreen === 'map') startMapUpdates();
    updateAllPositions();
    STATE.updateInterval = setInterval(updateAllPositions, 3000);
  }
});
