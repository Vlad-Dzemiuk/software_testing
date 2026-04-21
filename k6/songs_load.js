import * as k6Http from 'k6/http';
import { check as k6Check, sleep as k6Sleep } from 'k6';
import { Rate as K6Rate } from 'k6/metrics';

const http5xx = new K6Rate('http_5xx');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const GENRE = __ENV.GENRE || 'Rock';
const ARTIST = __ENV.ARTIST || '';

const VUS = Number(__ENV.VUS || 20);
const DURATION = __ENV.DURATION || '1m';

const P95_MS = Number(__ENV.P95_MS || 300);
const MAX_FAILED_RATE = Number(__ENV.MAX_FAILED_RATE || 0.01);
const MAX_5XX_RATE = Number(__ENV.MAX_5XX_RATE || 0.001);

export const options = {
  vus: VUS,
  duration: DURATION,
  thresholds: {
    http_req_failed: [`rate<${MAX_FAILED_RATE}`],
    http_5xx: [`rate<${MAX_5XX_RATE}`],
    http_req_duration: [`p(95)<${P95_MS}`],
  },
};

export default function () {
  const params = [];
  if (GENRE) params.push(`genre=${encodeURIComponent(GENRE)}`);
  if (ARTIST) params.push(`artist=${encodeURIComponent(ARTIST)}`);

  const url = `${BASE_URL}/api/songs${params.length ? `?${params.join('&')}` : ''}`;
  const res = k6Http.get(url, { tags: { name: 'GET /api/songs' } });

  http5xx.add(res.status >= 500);

  k6Check(res, {
    'status is 200': (r) => r.status === 200,
    'body is json array': (r) => {
      try {
        const body = r.json();
        return Array.isArray(body);
      } catch (_) {
        return false;
      }
    },
  });

  k6Sleep(0.2);
}
