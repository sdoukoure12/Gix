/* ============================================================
   Gix – Œil Satellites
   Web prototype: real-time satellite tracking + 2D sky view
   Data: CelesTrak (TLE) | Propagation: satellite.js (SGP4)
   ============================================================ */

'use strict';

// ── Configuration ─────────────────────────────────────────────
const CONFIG = {
  updateIntervalMs: 5000,
  celestrakUrl: 'https://celestrak.org/GP.php?GROUP=visual&FORMAT=json',
  maxSatellites: 60,
  defaultLat: 48.8566,   // Paris
  defaultLon:  2.3522,
  defaultAlt:  0.0,      // km above sea level
};

// ── Bright-star catalogue (subset of HYG / Hipparcos)
//    ra/dec in degrees, mag = apparent visual magnitude
// ─────────────────────────────────────────────────────────────
const BRIGHT_STARS = [
  { name: 'Sirius',         ra: 101.287, dec: -16.716, mag: -1.46 },
  { name: 'Canopus',        ra:  95.988, dec: -52.696, mag: -0.74 },
  { name: 'Arcturus',       ra: 213.915, dec:  19.182, mag: -0.05 },
  { name: 'α Centauri',     ra: 219.899, dec: -60.833, mag: -0.01 },
  { name: 'Vega',           ra: 279.234, dec:  38.783, mag:  0.03 },
  { name: 'Capella',        ra:  79.172, dec:  45.998, mag:  0.08 },
  { name: 'Rigel',          ra:  78.634, dec:  -8.202, mag:  0.12 },
  { name: 'Procyon',        ra: 114.825, dec:   5.225, mag:  0.34 },
  { name: 'Achernar',       ra:  24.428, dec: -57.237, mag:  0.46 },
  { name: 'Betelgeuse',     ra:  88.793, dec:   7.407, mag:  0.50 },
  { name: 'Hadar',          ra: 210.956, dec: -60.373, mag:  0.61 },
  { name: 'Acrux',          ra: 186.650, dec: -63.099, mag:  0.76 },
  { name: 'Altair',         ra: 297.696, dec:   8.868, mag:  0.76 },
  { name: 'Aldebaran',      ra:  68.980, dec:  16.509, mag:  0.85 },
  { name: 'Antares',        ra: 247.352, dec: -26.432, mag:  0.96 },
  { name: 'Spica',          ra: 201.298, dec: -11.161, mag:  0.97 },
  { name: 'Pollux',         ra: 116.329, dec:  28.026, mag:  1.14 },
  { name: 'Fomalhaut',      ra: 344.413, dec: -29.622, mag:  1.16 },
  { name: 'Deneb',          ra: 310.358, dec:  45.280, mag:  1.25 },
  { name: 'Mimosa',         ra: 191.930, dec: -59.689, mag:  1.25 },
  { name: 'Regulus',        ra: 152.093, dec:  11.967, mag:  1.35 },
  { name: 'Adhara',         ra: 104.656, dec: -28.972, mag:  1.50 },
  { name: 'Castor',         ra: 113.650, dec:  31.889, mag:  1.58 },
  { name: 'Shaula',         ra: 263.402, dec: -37.104, mag:  1.62 },
  { name: 'Bellatrix',      ra:  81.283, dec:   6.350, mag:  1.64 },
  { name: 'Gacrux',         ra: 187.792, dec: -57.113, mag:  1.64 },
  { name: 'Elnath',         ra:  81.573, dec:  28.608, mag:  1.65 },
  { name: 'Alnilam',        ra:  84.053, dec:  -1.202, mag:  1.69 },
  { name: 'Alnitak',        ra:  85.190, dec:  -1.943, mag:  1.74 },
  { name: 'Alioth',         ra: 193.507, dec:  55.960, mag:  1.76 },
  { name: 'Dubhe',          ra: 165.932, dec:  61.751, mag:  1.79 },
  { name: 'Mirfak',         ra:  51.081, dec:  49.861, mag:  1.79 },
  { name: 'Kaus Australis', ra: 276.043, dec: -34.385, mag:  1.85 },
  { name: 'Alkaid',         ra: 206.885, dec:  49.313, mag:  1.85 },
  { name: 'Avior',          ra: 125.629, dec: -59.510, mag:  1.86 },
  { name: 'Sargas',         ra: 264.330, dec: -42.998, mag:  1.86 },
  { name: 'Wezen',          ra: 107.098, dec: -26.393, mag:  1.83 },
  { name: 'Menkalinan',     ra:  89.882, dec:  44.947, mag:  1.90 },
  { name: 'Atria',          ra: 252.166, dec: -69.028, mag:  1.91 },
  { name: 'Alhena',         ra:  99.428, dec:  16.400, mag:  1.93 },
  { name: 'Peacock',        ra: 306.412, dec: -56.735, mag:  1.94 },
  { name: 'Alsephina',      ra: 131.176, dec: -54.709, mag:  1.95 },
  { name: 'Mirzam',         ra:  95.675, dec: -17.956, mag:  1.98 },
  { name: 'Polaris',        ra:  37.954, dec:  89.264, mag:  1.97 },
  { name: 'Alphard',        ra: 141.897, dec:  -8.659, mag:  1.98 },
  { name: 'Hamal',          ra:  31.793, dec:  23.463, mag:  2.00 },
  { name: 'Diphda',         ra:  10.897, dec: -17.987, mag:  2.04 },
  { name: 'Nunki',          ra: 283.816, dec: -26.297, mag:  2.05 },
  { name: 'Alnair',         ra: 332.058, dec: -46.961, mag:  1.73 },
  { name: 'Miaplacidus',    ra: 138.300, dec: -69.717, mag:  1.67 },
  { name: 'Regor',          ra: 122.383, dec: -47.337, mag:  1.70 },
];

// ── Fallback TLE data (demonstration – approximate epochs)
//    Used when CelesTrak is unreachable (e.g. CORS restriction).
// ─────────────────────────────────────────────────────────────
const FALLBACK_TLES = [
  {
    OBJECT_NAME: 'ISS (ZARYA)',
    TLE_LINE1: '1 25544U 98067A   24365.50000000  .00021384  00000-0  38580-3 0  9990',
    TLE_LINE2: '2 25544  51.6399 297.7282 0002088  93.8044 266.3382 15.49748472428929',
  },
  {
    OBJECT_NAME: 'HUBBLE SPACE TELESCOPE',
    TLE_LINE1: '1 20580U 90037B   24365.50000000  .00001022  00000-0  37267-4 0  9990',
    TLE_LINE2: '2 20580  28.4706 324.7568 0002668 255.8337 103.5483 15.09620817 38940',
  },
  {
    OBJECT_NAME: 'STARLINK-1007',
    TLE_LINE1: '1 44713U 19074A   24365.50000000  .00001234  00000-0  95433-4 0  9990',
    TLE_LINE2: '2 44713  53.0000 120.0000 0001200  90.0000 270.0000 15.06420000 12345',
  },
  {
    OBJECT_NAME: 'STARLINK-2030',
    TLE_LINE1: '1 47528U 21006AF  24365.50000000  .00001500  00000-0  11200-3 0  9990',
    TLE_LINE2: '2 47528  53.0000 150.0000 0001200  45.0000 315.0000 15.06420000 23456',
  },
  {
    OBJECT_NAME: 'NOAA 18',
    TLE_LINE1: '1 28654U 05018A   24365.50000000  .00000034  00000-0  33714-4 0  9990',
    TLE_LINE2: '2 28654  98.7074 125.5760 0014148  56.9219 303.3497 14.10617520968895',
  },
  {
    OBJECT_NAME: 'TERRA',
    TLE_LINE1: '1 25994U 99068A   24365.50000000  .00000054  00000-0  29050-4 0  9990',
    TLE_LINE2: '2 25994  98.1987 272.9720 0001306 108.0460 252.0876 14.57116523285849',
  },
  {
    OBJECT_NAME: 'AQUA',
    TLE_LINE1: '1 27424U 02022A   24365.50000000  .00000069  00000-0  35745-4 0  9990',
    TLE_LINE2: '2 27424  98.2115 294.5700 0001600  95.7824 264.3543 14.57216437160940',
  },
  {
    OBJECT_NAME: 'SENTINEL-2A',
    TLE_LINE1: '1 40697U 15028A   24365.50000000 -.00000030  00000-0  21060-4 0  9990',
    TLE_LINE2: '2 40697  98.5690 165.4850 0001283  95.5720 264.5600 14.30822890469540',
  },
  {
    OBJECT_NAME: 'GOES-16',
    TLE_LINE1: '1 41866U 16071A   24365.50000000 -.00000266  00000-0  00000-0 0  9990',
    TLE_LINE2: '2 41866   0.0512 285.3600 0000741 285.3700  78.4800  1.00273367 28571',
  },
  {
    OBJECT_NAME: 'GPS BIIR-2',
    TLE_LINE1: '1 24876U 97035A   24365.50000000 -.00000022  00000-0  00000-0 0  9990',
    TLE_LINE2: '2 24876  55.9419 283.9975 0046601 117.8694 242.6468  2.00564289197614',
  },
];

// ── Application state ──────────────────────────────────────────
const state = {
  satellites: [],
  selectedId: null,
  observer: {
    lat: CONFIG.defaultLat,
    lon: CONFIG.defaultLon,
    alt: CONFIG.defaultAlt,
    label: 'Paris, France',
  },
  view: 'map',
  map: null,
  observerMarker: null,
  markers: {},
  skyCanvas: null,
  skyCtx: null,
  skyBgStars: null,
  updateTimer: null,
  usingFallback: false,
};

// ── Astronomical helpers ───────────────────────────────────────

/** Convert a Date to Julian Day Number. */
function dateToJulian(date) {
  return date.getTime() / 86400000.0 + 2440587.5;
}

/**
 * Convert equatorial coordinates (RA/Dec) to horizontal coordinates
 * (Altitude / Azimuth) for a given observer and time.
 * @param {number} ra   – Right Ascension in degrees
 * @param {number} dec  – Declination in degrees
 * @param {number} lat  – Observer latitude in degrees
 * @param {number} lon  – Observer longitude in degrees
 * @param {Date}   date – UTC date/time
 * @returns {{ alt: number, az: number }} degrees
 */
function raDecToAltAz(ra, dec, lat, lon, date) {
  const D2R = Math.PI / 180;
  const R2D = 180 / Math.PI;

  const JD = dateToJulian(date);
  const T  = (JD - 2451545.0) / 36525.0;

  // Greenwich Mean Sidereal Time (degrees)
  let GMST = 280.46061837
    + 360.98564736629 * (JD - 2451545.0)
    + 0.000387933 * T * T
    - T * T * T / 38710000.0;
  GMST = ((GMST % 360) + 360) % 360;

  const LST = (GMST + lon + 360) % 360;
  const HA  = (LST - ra + 360) % 360;

  const haR  = HA  * D2R;
  const decR = dec * D2R;
  const latR = lat * D2R;

  const sinAlt = Math.sin(decR) * Math.sin(latR)
               + Math.cos(decR) * Math.cos(latR) * Math.cos(haR);
  const alt = Math.asin(Math.max(-1, Math.min(1, sinAlt))) * R2D;

  const cosAlt = Math.cos(alt * D2R);
  const cosAz  = cosAlt > 1e-9
    ? (Math.sin(decR) - Math.sin(latR) * sinAlt) / (Math.cos(latR) * cosAlt)
    : 0;
  let az = Math.acos(Math.max(-1, Math.min(1, cosAz))) * R2D;
  if (Math.sin(haR) > 0) az = 360 - az;

  return { alt, az };
}

// ── Orbital propagation ────────────────────────────────────────

/**
 * Compute the current position and look-angles of a satellite.
 * Returns null if propagation fails.
 */
function propagate(sat, date) {
  if (!sat.satrec) return null;
  try {
    const pv = satellite.propagate(sat.satrec, date);
    if (!pv || !pv.position || pv.position === true) return null;

    const gmst   = satellite.gstime(date);
    const posGd  = satellite.eciToGeodetic(pv.position, gmst);
    const lat    = satellite.degreesLat(posGd.latitude);
    const lon    = satellite.degreesLong(posGd.longitude);
    const alt    = posGd.height; // km
    const vel    = pv.velocity;
    const speed  = Math.sqrt(vel.x * vel.x + vel.y * vel.y + vel.z * vel.z);

    // Look-angles from observer
    const posEcf = satellite.eciToEcf(pv.position, gmst);
    const obsGd  = {
      longitude: state.observer.lon * Math.PI / 180,
      latitude:  state.observer.lat * Math.PI / 180,
      height:    state.observer.alt,
    };
    const look     = satellite.ecfToLookAngles(obsGd, posEcf);
    const elevation = look.elevation * 180 / Math.PI;
    const azimuth   = look.azimuth   * 180 / Math.PI;

    return { lat, lon, alt, speed, elevation, azimuth };
  } catch (err) {
    console.debug('[Gix] propagation error for', sat.name, err.message);
    return null;
  }
}

// ── Data fetching ──────────────────────────────────────────────

async function fetchSatellites() {
  setStatus('Récupération des données satellites depuis CelesTrak…');
  let tleData = null;

  try {
    const resp = await fetch(CONFIG.celestrakUrl);
    if (resp.ok) {
      tleData = await resp.json();
    }
  } catch (err) {
    // Network / CORS – fall through to fallback
    console.warn('[Gix] CelesTrak fetch failed:', err.message);
  }

  if (!Array.isArray(tleData) || tleData.length === 0) {
    tleData = FALLBACK_TLES;
    state.usingFallback = true;
    setStatus('Données de démonstration (CelesTrak inaccessible depuis le navigateur)');
  } else {
    setStatus(`${tleData.length} satellites chargés depuis CelesTrak`);
  }

  const slice = tleData.slice(0, CONFIG.maxSatellites);

  state.satellites = slice
    .map((tle, i) => {
      let satrec = null;
      try { satrec = satellite.twoline2satrec(tle.TLE_LINE1, tle.TLE_LINE2); } catch (err) {
        console.warn('[Gix] TLE parse error for', tle.OBJECT_NAME, err.message);
      }
      if (!satrec) return null;

      // Derive orbital elements from satrec when not present in JSON
      const inclDeg = tle.INCLINATION !== undefined
        ? parseFloat(tle.INCLINATION)
        : satrec.inclo * 180 / Math.PI;
      const noRevDay = tle.MEAN_MOTION !== undefined
        ? parseFloat(tle.MEAN_MOTION)
        : satrec.no * (1440 / (2 * Math.PI));
      const periodMin = noRevDay > 0 ? 1440 / noRevDay : 0;

      // Unique colour per satellite (golden-angle hue distribution)
      const hue   = (i * 137.508) % 360;
      const color = `hsl(${hue.toFixed(0)}, 78%, 62%)`;

      return {
        id: i,
        name: (tle.OBJECT_NAME || tle.SATNAME || 'Inconnu').trim(),
        satrec,
        inclination: inclDeg,
        period: periodMin,
        color,
        position: null,
      };
    })
    .filter(Boolean);

  updatePositions();
  renderSatelliteList();
  hideLoading();
}

// ── Position update loop ───────────────────────────────────────

function updatePositions() {
  const now = new Date();
  state.satellites.forEach(sat => { sat.position = propagate(sat, now); });

  updateMap();
  updateSkyView();
  updateInfoPanel();
  renderSatelliteList();

  document.getElementById('last-update').textContent =
    'Mis à jour : ' + now.toLocaleTimeString('fr-FR');
}

// ── World map (Leaflet) ────────────────────────────────────────

function initMap() {
  state.map = L.map('map', {
    center: [20, 0],
    zoom: 2,
    minZoom: 1,
    maxZoom: 10,
    zoomControl: true,
  });

  // CartoDB Dark Matter tile layer – no CSS filter needed
  L.tileLayer(
    'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
    {
      attribution:
        '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' +
        ' &copy; <a href="https://carto.com/">CARTO</a>' +
        ' | Satellites: <a href="https://celestrak.org">CelesTrak</a>',
      subdomains: 'abcd',
      maxZoom: 20,
    }
  ).addTo(state.map);

  // Observer marker
  state.observerMarker = L.circleMarker(
    [state.observer.lat, state.observer.lon],
    { radius: 7, color: '#00d4ff', fillColor: '#00d4ff', fillOpacity: 0.85, weight: 2 }
  )
    .addTo(state.map)
    .bindTooltip('📍 ' + state.observer.label, { permanent: false, direction: 'top' });
}

function makeSatIcon(color) {
  return L.divIcon({
    className: '',
    html: `<div style="
      width:10px;height:10px;
      background:${color};
      border-radius:50%;
      border:1.5px solid #fff;
      box-shadow:0 0 6px ${color};
    "></div>`,
    iconSize: [10, 10],
    iconAnchor: [5, 5],
  });
}

function updateMap() {
  if (!state.map) return;

  state.satellites.forEach(sat => {
    if (!sat.position) return;
    const { lat, lon, alt, speed } = sat.position;

    const tooltip = `<strong>${sat.name}</strong><br>` +
      `Alt : ${alt.toFixed(0)} km &nbsp;|&nbsp; ` +
      `${lat.toFixed(2)}°, ${lon.toFixed(2)}°<br>` +
      `Vitesse : ${speed.toFixed(2)} km/s`;

    if (state.markers[sat.id]) {
      state.markers[sat.id].setLatLng([lat, lon]);
      state.markers[sat.id].setTooltipContent(tooltip);
    } else {
      const m = L.marker([lat, lon], { icon: makeSatIcon(sat.color) })
        .addTo(state.map)
        .bindTooltip(tooltip, { direction: 'top', offset: [0, -8] });
      m.on('click', () => selectSatellite(sat.id));
      state.markers[sat.id] = m;
    }
  });
}

// ── 2-D Sky View (Canvas) ──────────────────────────────────────

function initSkyView() {
  state.skyCanvas = document.getElementById('sky-canvas');
  state.skyCtx    = state.skyCanvas.getContext('2d');
  resizeSkyCanvas();
  window.addEventListener('resize', resizeSkyCanvas);

  state.skyCanvas.addEventListener('click', onSkyClick);
}

function resizeSkyCanvas() {
  const c = state.skyCanvas;
  const p = document.getElementById('sky-view');
  c.width  = p.clientWidth;
  c.height = p.clientHeight;
  state.skyBgStars = null; // regenerate on next draw
  if (state.view === 'sky') drawSkyView();
}

/** Project altitude (0–90°) and azimuth (0–360°, N=0) to canvas xy. */
function altAzToXY(alt, az, cx, cy, R) {
  const r   = (1 - alt / 90) * R;
  const rad = (az - 90) * Math.PI / 180; // rotate so N is up
  return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
}

function generateBgStars(R) {
  return Array.from({ length: 250 }, () => ({
    angle: Math.random() * Math.PI * 2,
    r:     Math.random() * R,
    size:  Math.random() * 0.7 + 0.2,
    alpha: Math.random() * 0.35 + 0.05,
  }));
}

function drawSkyView() {
  const canvas = state.skyCanvas;
  const ctx    = state.skyCtx;
  const W = canvas.width, H = canvas.height;
  const cx = W / 2, cy = H / 2;
  const R  = Math.min(W, H) * 0.44;
  const now = new Date();

  ctx.clearRect(0, 0, W, H);

  // ── Sky gradient background ──
  const grad = ctx.createRadialGradient(cx, cy, 0, cx, cy, R);
  grad.addColorStop(0,   '#0b1730');
  grad.addColorStop(0.7, '#070c18');
  grad.addColorStop(1,   '#030810');
  ctx.save();
  ctx.beginPath();
  ctx.arc(cx, cy, R, 0, Math.PI * 2);
  ctx.fillStyle = grad;
  ctx.fill();
  ctx.restore();

  // ── Clip everything inside the circle ──
  ctx.save();
  ctx.beginPath();
  ctx.arc(cx, cy, R, 0, Math.PI * 2);
  ctx.clip();

  // ── Random background stars ──
  if (!state.skyBgStars) state.skyBgStars = generateBgStars(R);
  state.skyBgStars.forEach(s => {
    const x = cx + s.r * Math.cos(s.angle);
    const y = cy + s.r * Math.sin(s.angle);
    ctx.beginPath();
    ctx.arc(x, y, s.size, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(180,200,255,${s.alpha})`;
    ctx.fill();
  });

  // ── Altitude grid rings (0°, 30°, 60°) ──
  ctx.strokeStyle = 'rgba(255,255,255,0.07)';
  ctx.lineWidth   = 1;
  [0, 30, 60].forEach(alt => {
    const r = (1 - alt / 90) * R;
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.stroke();
  });

  ctx.restore(); // end clip

  // ── Horizon border ──
  ctx.strokeStyle = 'rgba(0,212,255,0.4)';
  ctx.lineWidth   = 1.5;
  ctx.beginPath();
  ctx.arc(cx, cy, R, 0, Math.PI * 2);
  ctx.stroke();

  // ── Altitude labels ──
  ctx.fillStyle  = 'rgba(255,255,255,0.22)';
  ctx.font       = '10px monospace';
  ctx.textAlign  = 'left';
  ctx.textBaseline = 'middle';
  [0, 30, 60].forEach(alt => {
    const r = (1 - alt / 90) * R;
    ctx.fillText(`${alt}°`, cx + r + 4, cy);
  });

  // ── Cardinal points ──
  const cardinals = [
    { label: 'N', az: 0 }, { label: 'E', az: 90 },
    { label: 'S', az: 180 }, { label: 'O', az: 270 },
  ];
  ctx.fillStyle    = 'rgba(0,212,255,0.7)';
  ctx.font         = 'bold 13px sans-serif';
  ctx.textAlign    = 'center';
  ctx.textBaseline = 'middle';
  cardinals.forEach(({ label, az }) => {
    const rad = (az - 90) * Math.PI / 180;
    ctx.fillText(label, cx + (R + 16) * Math.cos(rad), cy + (R + 16) * Math.sin(rad));
  });

  // ── Catalogue stars ──
  BRIGHT_STARS.forEach(star => {
    const pos = raDecToAltAz(star.ra, star.dec,
                              state.observer.lat, state.observer.lon, now);
    if (pos.alt < 0) return;

    const { x, y } = altAzToXY(pos.alt, pos.az, cx, cy, R);
    const size  = Math.max(0.5, 3.6 - star.mag * 0.75);
    const alpha = Math.min(1, Math.max(0.3, 1.1 - star.mag * 0.12));

    ctx.beginPath();
    ctx.arc(x, y, size, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(200,218,255,${alpha.toFixed(2)})`;
    ctx.fill();
  });

  // ── Satellites above horizon ──
  let visibleCount = 0;
  state.satellites.forEach(sat => {
    if (!sat.position) return;
    const { elevation, azimuth } = sat.position;
    if (elevation < 0) return;
    visibleCount++;

    const { x, y } = altAzToXY(elevation, azimuth, cx, cy, R);
    const isActive  = sat.id === state.selectedId;
    const dotR      = isActive ? 6 : 4;

    if (isActive) {
      ctx.beginPath();
      ctx.arc(x, y, 12, 0, Math.PI * 2);
      ctx.fillStyle = sat.color + '30';
      ctx.fill();
    }

    ctx.beginPath();
    ctx.arc(x, y, dotR, 0, Math.PI * 2);
    ctx.fillStyle   = sat.color;
    ctx.strokeStyle = 'rgba(255,255,255,0.8)';
    ctx.lineWidth   = 1;
    ctx.fill();
    ctx.stroke();

    // Label
    ctx.fillStyle    = sat.color;
    ctx.font         = `${isActive ? 'bold ' : ''}10px sans-serif`;
    ctx.textAlign    = 'left';
    ctx.textBaseline = 'bottom';
    ctx.fillText(sat.name, x + 9, y - 2);
  });

  // ── Zenith crosshair ──
  ctx.strokeStyle = 'rgba(255,255,255,0.15)';
  ctx.lineWidth   = 1;
  ctx.beginPath();
  ctx.moveTo(cx - 10, cy); ctx.lineTo(cx + 10, cy);
  ctx.moveTo(cx, cy - 10); ctx.lineTo(cx, cy + 10);
  ctx.stroke();

  // ── Sky info bar ──
  document.getElementById('sky-time').textContent =
    '🕐 ' + now.toUTCString().slice(17, 25) + ' UTC';
  document.getElementById('sky-observer').textContent =
    `📍 ${state.observer.lat.toFixed(2)}°, ${state.observer.lon.toFixed(2)}°`;
  document.getElementById('sky-visible-count').textContent =
    `🛰️ ${visibleCount} visible${visibleCount !== 1 ? 's' : ''}`;
}

function onSkyClick(e) {
  const rect  = state.skyCanvas.getBoundingClientRect();
  const scaleX = state.skyCanvas.width  / rect.width;
  const scaleY = state.skyCanvas.height / rect.height;
  const clickX = (e.clientX - rect.left) * scaleX;
  const clickY = (e.clientY - rect.top)  * scaleY;

  const W = state.skyCanvas.width, H = state.skyCanvas.height;
  const cx = W / 2, cy = H / 2;
  const R  = Math.min(W, H) * 0.44;

  let closest  = null;
  let minDist  = 22; // px threshold

  state.satellites.forEach(sat => {
    if (!sat.position || sat.position.elevation < 0) return;
    const { x, y } = altAzToXY(sat.position.elevation, sat.position.azimuth, cx, cy, R);
    const d = Math.hypot(x - clickX, y - clickY);
    if (d < minDist) { minDist = d; closest = sat; }
  });

  if (closest) selectSatellite(closest.id);
}

function updateSkyView() {
  if (state.view === 'sky') drawSkyView();
}

// ── Satellite list sidebar ─────────────────────────────────────

function renderSatelliteList() {
  document.getElementById('satellite-count').textContent = state.satellites.length;
  const list = document.getElementById('satellite-list');

  list.innerHTML = state.satellites.map(sat => {
    const pos     = sat.position;
    const altText = pos ? `${pos.alt.toFixed(0)} km` : '—';
    const above   = pos && pos.elevation > 0
      ? `<span class="sat-above"> ▲ ${pos.elevation.toFixed(0)}°</span>`
      : '';
    const cls = sat.id === state.selectedId ? ' active' : '';
    return `<div class="satellite-item${cls}" role="listitem"
                 onclick="selectSatellite(${sat.id})"
                 title="${sat.name}">
      <div class="sat-dot" style="background:${sat.color};box-shadow:0 0 4px ${sat.color}"></div>
      <div class="sat-info">
        <div class="sat-name">${sat.name}</div>
        <div class="sat-alt">${altText}${above}</div>
      </div>
    </div>`;
  }).join('');
}

// ── Satellite selection ────────────────────────────────────────

function selectSatellite(id) {
  state.selectedId = id;
  renderSatelliteList();
  updateInfoPanel();

  const sat = state.satellites.find(s => s.id === id);
  if (sat && sat.position && state.map && state.view === 'map') {
    const z = Math.max(state.map.getZoom(), 4);
    state.map.setView([sat.position.lat, sat.position.lon], z);
  }
  if (state.view === 'sky') drawSkyView();
}

function updateInfoPanel() {
  const panel = document.getElementById('info-panel');
  if (state.selectedId === null) { panel.classList.add('hidden'); return; }

  const sat = state.satellites.find(s => s.id === state.selectedId);
  if (!sat) { panel.classList.add('hidden'); return; }

  panel.classList.remove('hidden');
  document.getElementById('info-name').textContent = '🛰️ ' + sat.name;

  const pos = sat.position;
  document.getElementById('info-altitude').textContent =
    pos ? `${pos.alt.toFixed(0)} km` : '—';
  document.getElementById('info-lat').textContent =
    pos ? `${pos.lat.toFixed(3)}°` : '—';
  document.getElementById('info-lon').textContent =
    pos ? `${pos.lon.toFixed(3)}°` : '—';
  document.getElementById('info-velocity').textContent =
    pos ? `${pos.speed.toFixed(2)} km/s` : '—';
  document.getElementById('info-inclination').textContent =
    `${sat.inclination.toFixed(2)}°`;
  document.getElementById('info-period').textContent =
    sat.period > 0 ? `${sat.period.toFixed(1)} min` : '—';
  document.getElementById('info-elevation').textContent =
    pos ? `${pos.elevation.toFixed(1)}°` : '—';
  document.getElementById('info-azimuth').textContent =
    pos ? `${pos.azimuth.toFixed(1)}°` : '—';
}

// ── Observer geolocation ───────────────────────────────────────

function locateObserver() {
  if (!navigator.geolocation) {
    setStatus('La géolocalisation n\'est pas disponible dans ce navigateur.');
    return;
  }
  setStatus('Détection de votre position…');
  navigator.geolocation.getCurrentPosition(
    pos => {
      state.observer.lat   = pos.coords.latitude;
      state.observer.lon   = pos.coords.longitude;
      state.observer.alt   = (pos.coords.altitude || 0) / 1000;
      state.observer.label =
        `${pos.coords.latitude.toFixed(2)}°, ${pos.coords.longitude.toFixed(2)}°`;

      document.getElementById('observer-label').textContent =
        '📍 ' + state.observer.label;

      if (state.observerMarker) {
        state.observerMarker
          .setLatLng([state.observer.lat, state.observer.lon])
          .setTooltipContent('📍 ' + state.observer.label);
      }
      state.skyBgStars = null;
      updatePositions();
      setStatus('Position de l\'observateur mise à jour');
    },
    () => setStatus('Impossible d\'obtenir votre position GPS')
  );
}

// ── Tab navigation ─────────────────────────────────────────────

function initTabs() {
  document.querySelectorAll('.tab').forEach(btn => {
    btn.addEventListener('click', () => {
      const viewId = btn.dataset.view;
      state.view = viewId;

      document.querySelectorAll('.tab').forEach(b => {
        b.classList.remove('active');
        b.setAttribute('aria-selected', 'false');
      });
      btn.classList.add('active');
      btn.setAttribute('aria-selected', 'true');

      document.querySelectorAll('.view').forEach(v => {
        v.classList.remove('active');
        v.hidden = true;
      });
      const panel = document.getElementById(`${viewId}-view`);
      panel.classList.add('active');
      panel.hidden = false;

      if (viewId === 'sky') {
        resizeSkyCanvas();
        drawSkyView();
      } else if (viewId === 'map' && state.map) {
        state.map.invalidateSize();
      }
    });
  });
}

// ── UI helpers ─────────────────────────────────────────────────

function setStatus(msg) {
  document.getElementById('status-msg').textContent = msg;
}

function hideLoading() {
  const el = document.getElementById('loading-overlay');
  el.style.opacity    = '0';
  el.style.transition = 'opacity 0.5s';
  setTimeout(() => { el.style.display = 'none'; }, 500);
}

// ── Bootstrap ─────────────────────────────────────────────────

async function init() {
  initTabs();
  initMap();
  initSkyView();

  document.getElementById('locate-btn').addEventListener('click', locateObserver);

  document.getElementById('close-info').addEventListener('click', () => {
    state.selectedId = null;
    document.getElementById('info-panel').classList.add('hidden');
    renderSatelliteList();
    if (state.view === 'sky') drawSkyView();
  });

  await fetchSatellites();

  // Periodic position refresh
  state.updateTimer = setInterval(updatePositions, CONFIG.updateIntervalMs);
}

window.addEventListener('DOMContentLoaded', init);
