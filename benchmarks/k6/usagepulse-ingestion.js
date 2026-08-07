import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.USAGEPULSE_BASE_URL;
const tenantId = __ENV.USAGEPULSE_TENANT_ID || 'tenant-a';

if (!baseUrl) {
  throw new Error('USAGEPULSE_BASE_URL is required.');
}

export const options = {
  stages: [
    { duration: '2m', target: 50 },
    { duration: '5m', target: 200 },
    { duration: '2m', target: 0 }
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800']
  }
};

export default function () {
  const response = http.get(`${baseUrl}/api/dashboard/${tenantId}/realtime?window=5m`);

  check(response, {
    'status ok': (r) => r.status === 200
  });

  sleep(0.2);
}
