import * as k6Http from 'k6/http';
import { check as k6Check, sleep as k6Sleep } from 'k6';
import { Rate as K6Rate } from 'k6/metrics';

const http5xx = new K6Rate('http_5xx');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const USER_ID = __ENV.USER_ID || 'k6-user';

// If provided, setup() is skipped and these are used directly.
const PLAYLIST_ID_ENV = __ENV.PLAYLIST_ID ? Number(__ENV.PLAYLIST_ID) : null;
const SONG_IDS_ENV = (__ENV.SONG_IDS || '')
  .split(',')
  .map((s) => s.trim())
  .filter(Boolean)
  .map((s) => Number(s));

const STAGES = __ENV.STAGES || '30s:5,30s:10,30s:20,30s:30,30s:40';
const P95_MS = Number(__ENV.P95_MS || 800);
const MAX_5XX_RATE = Number(__ENV.MAX_5XX_RATE || 0.01);

function parseStages(stagesStr) {
  return stagesStr.split(',').map((pair) => {
    const [duration, target] = pair.split(':');
    return { duration: duration.trim(), target: Number(target) };
  });
}

export const options = {
  stages: parseStages(STAGES),
  thresholds: {
    http_5xx: [`rate<${MAX_5XX_RATE}`],
    http_req_duration: [`p(95)<${P95_MS}`],
  },
};

function apiPostJson(url, body, extraHeaders = {}) {
  return k6Http.post(url, JSON.stringify(body), {
    headers: {
      'Content-Type': 'application/json',
      ...extraHeaders,
    },
  });
}

function apiPutJson(url, body, extraHeaders = {}) {
  return k6Http.put(url, JSON.stringify(body), {
    headers: {
      'Content-Type': 'application/json',
      ...extraHeaders,
    },
  });
}

function shuffleCopy(arr) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

export function setup() {
  if (PLAYLIST_ID_ENV && SONG_IDS_ENV.length >= 2) {
    return { playlistId: PLAYLIST_ID_ENV, songIds: SONG_IDS_ENV, userId: USER_ID };
  }

  // Create multiple playlists to avoid VUs contending on the same playlist.
  // Stress should surface performance under load, not explode into mass 5xx due to write-write races.
  const songCount = Number(__ENV.SONG_COUNT || 20);
  const playlistCount = Number(__ENV.PLAYLISTS || 40);
  const songIds = [];

  for (let i = 0; i < songCount; i++) {
    const createSong = {
      title: `k6-song-${i}-${Date.now()}`,
      artist: 'k6-artist',
      album: 'k6-album',
      durationSeconds: 120,
      genre: 'Rock',
      releaseDate: '2020-01-01',
    };

    const r = apiPostJson(`${BASE_URL}/api/songs`, createSong);
    k6Check(r, { 'setup: create song 201': (x) => x.status === 201 });
    if (r.status !== 201) {
      throw new Error(`setup: failed to create song: status=${r.status} body=${r.body}`);
    }
    songIds.push(r.json().id);
  }

  const playlistIds = [];
  for (let pIdx = 0; pIdx < playlistCount; pIdx++) {
    const createPlaylistReq = { name: `k6-playlist-${pIdx}-${Date.now()}`, description: null, isPublic: false };
    const p = apiPostJson(`${BASE_URL}/api/playlists`, createPlaylistReq, { 'X-User-Id': USER_ID });
    k6Check(p, { 'setup: create playlist 201': (x) => x.status === 201 });
    if (p.status !== 201) {
      throw new Error(`setup: failed to create playlist: status=${p.status} body=${p.body}`);
    }
    const playlistId = p.json().id;
    playlistIds.push(playlistId);

    for (const sid of songIds) {
      const add = apiPostJson(
        `${BASE_URL}/api/playlists/${playlistId}/songs`,
        { songId: sid },
        { 'X-User-Id': USER_ID }
      );
      k6Check(add, { 'setup: add song 204': (x) => x.status === 204 });
      if (add.status !== 204) {
        throw new Error(`setup: failed to add song: status=${add.status} body=${add.body}`);
      }
    }
  }

  return { playlistIds, songIds, userId: USER_ID };
}

export default function (data) {
  const playlistIds = data.playlistIds || [];
  const playlistId = playlistIds.length ? playlistIds[(__VU - 1) % playlistIds.length] : data.playlistId;
  const songIds = data.songIds;

  const shuffled = shuffleCopy(songIds);
  const items = shuffled.map((songId, idx) => ({ songId, position: idx + 1 }));
  const body = { items };

  const res = apiPutJson(`${BASE_URL}/api/playlists/${playlistId}/reorder`, body, {
    'X-User-Id': data.userId,
  });

  http5xx.add(res.status >= 500);

  // For stress runs, occasional 400/409 can be acceptable (race/validation),
  // but 5xx should stay very low.
  k6Check(res, {
    'status is 204/400/409': (r) => r.status === 204 || r.status === 400 || r.status === 409,
  });

  k6Sleep(0.1);
}
