import { createHash, randomUUID } from 'node:crypto';
import { createClient } from 'redis';
import type { AuthSessionDto, RefreshRequest } from './types';

let redisClient: ReturnType<typeof createClient> | undefined;
let connecting: Promise<unknown> | undefined;

function getRedisClient() {
  if (redisClient) return redisClient;

  const configuredRedisUrl = process.env.WEB_REDIS_URL?.trim();
  if (!configuredRedisUrl) {
    throw new Error('WEB_REDIS_URL is required for CompanyHQ Web. Set it in the active environment.');
  }

  redisClient = createClient({ url: configuredRedisUrl, socket: { connectTimeout: 3000, reconnectStrategy: false } });
  redisClient.on('error', () => {});
  return redisClient;
}

export async function coordinatedRefresh(api: string, refreshToken: string): Promise<AuthSessionDto | null> {
  const redis = getRedisClient();
  if (!redis.isReady) {
    connecting ??= redis.connect().finally(() => { connecting = undefined; });
    await connecting;
  }
  const key = 'companyhq:web:refresh:' + createHash('sha256').update(refreshToken).digest('hex');
  const lock = key + ':lock';
  const owner = randomUUID();
  for (let attempt = 0; attempt < 80; attempt++) {
    const cached = await redis.get(key);
    if (cached) return JSON.parse(cached) as AuthSessionDto | null;
    if (await redis.set(lock, owner, { NX: true, EX: 20 })) {
      try {
        // Recheck after acquiring: another worker may have completed between GET and SET.
        const completed = await redis.get(key);
        if (completed) return JSON.parse(completed) as AuthSessionDto | null;
        const body: RefreshRequest = { refreshToken };
        const response = await fetch(api + '/api/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), cache: 'no-store', signal: AbortSignal.timeout(10000) });
        if (!response.ok && ![400, 401, 403].includes(response.status)) throw new Error('Refresh unavailable');
        const session = response.ok ? await response.json() as AuthSessionDto : null;
        // Concurrent requests carry the same old cookie. Return the one rotated result to all of them.
        await redis.set(key, JSON.stringify(session), { EX: 30 });
        return session;
      } finally {
        await redis.eval("if redis.call('get',KEYS[1]) == ARGV[1] then return redis.call('del',KEYS[1]) end return 0", { keys: [lock], arguments: [owner] });
      }
    }
    await new Promise(resolve => setTimeout(resolve, 150));
  }
  throw new Error('Refresh unavailable');
}

