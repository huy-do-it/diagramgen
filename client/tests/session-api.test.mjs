import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import ts from 'typescript';

const source = relative => readFileSync(new URL(relative, import.meta.url), 'utf8');
function moduleUrl(code) {
  const js = ts.transpileModule(code, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
  return `data:text/javascript;base64,${Buffer.from(js).toString('base64')}`;
}
const sessionUrl = moduleUrl(source('../src/auth/session.ts'));
const apiUrl = moduleUrl(source('../src/shared/api.ts').replace("'../auth/session'", JSON.stringify(sessionUrl)));
const session = await import(sessionUrl);
const api = await import(apiUrl);
const diagram = await import(moduleUrl(source('../src/features/diagram/diagramApi.ts').replace("'../../shared/api'", JSON.stringify(apiUrl))));
const store = new Map();
globalThis.sessionStorage = {
  getItem: key => store.get(key) ?? null,
  setItem: (key, value) => store.set(key, value),
  removeItem: key => store.delete(key),
};
const tokens = new Map();
const token = label => {
  if (!tokens.has(label)) tokens.set(label, `e30.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, label })).toString('base64url')}.sig`);
  return tokens.get(label);
};
const login = label => session.saveSession(token(label), `refresh-${label}`, 3600);
const rotation = label => Response.json({ accessToken: token(label), refreshToken: `refresh-${label}`, expiresIn: 3600 });
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };

test('401 refreshes once and retries with the rotated token and unchanged request body', async () => {
  login('A');
  const calls = [];
  globalThis.fetch = async (url, init) => {
    calls.push({ url, init });
    if (url.endsWith('/auth/refresh')) return rotation('A2');
    return calls.length === 1 ? new Response(null, { status: 401 }) : Response.json({ ok: true });
  };
  const version = session.getSessionVersion();
  assert.deepEqual(await api.apiRequest('/projects', { method: 'POST', body: '{"name":"A"}' }), { ok: true });
  assert.equal(calls.length, 3);
  assert.equal(calls[2].init.headers.get('Authorization'), `Bearer ${token('A2')}`);
  assert.equal(calls[2].init.body, calls[0].init.body);
  assert.equal(session.getSessionVersion(), version);
  assert.ok(calls[1].init.signal);
});

test('refresh failure logs out; a second 401 never refreshes again', async () => {
  for (const refreshFails of [true, false]) {
    login('A');
    let refreshes = 0, requests = 0, logouts = 0;
    api.setUnauthorizedHandler(() => { logouts++; });
    globalThis.fetch = async url => {
      if (url.endsWith('/auth/refresh')) { refreshes++; return refreshFails ? new Response(null, { status: 401 }) : rotation('A2'); }
      requests++;
      return new Response(null, { status: 401 });
    };
    await assert.rejects(api.apiFetch('/users/me'), { status: 401 });
    assert.equal(refreshes, 1);
    assert.equal(requests, refreshFails ? 1 : 2);
    assert.equal(logouts, 1);
    assert.equal(session.readSession(), null);
  }
  api.setUnauthorizedHandler(undefined);
});

test('concurrent 401s share one refresh; delayed old-token 401 reuses the rotated token', async () => {
  login('A');
  const refresh = deferred(), late = deferred();
  let refreshes = 0;
  globalThis.fetch = async (url, init) => {
    if (url.endsWith('/auth/refresh')) { refreshes++; return refresh.promise; }
    if (init.headers.get('Authorization') === `Bearer ${token('A2')}`) return Response.json({ ok: true });
    if (url.endsWith('/late')) return late.promise;
    return new Response(null, { status: 401 });
  };
  const pending = [api.apiFetch('/first'), api.apiFetch('/second'), api.apiFetch('/late')];
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(refreshes, 1);
  refresh.resolve(rotation('A2'));
  await Promise.all(pending.slice(0, 2));
  late.resolve(new Response(null, { status: 401 }));
  await pending[2];
  assert.equal(refreshes, 1);
});

test('a late refresh cannot overwrite a new login or resurrect logout', async () => {
  for (const logout of [false, true]) {
    login('A');
    const pending = deferred();
    globalThis.fetch = async () => pending.promise;
    const refresh = api.getOrQueueRefreshSession();
    if (logout) session.clearSession(); else login('B');
    pending.resolve(rotation('A2'));
    assert.equal(await refresh, null);
    assert.equal(session.readSession()?.refreshToken ?? null, logout ? null : 'refresh-B');
  }
});

test('old requests neither refresh/retry as a new account nor clear its session', async () => {
  for (const status of [200, 401]) {
    login('A');
    const pending = deferred();
    let requests = 0;
    globalThis.fetch = async () => { requests++; return pending.promise; };
    const request = api.apiFetch('/projects', { method: 'POST', body: '{}' });
    login('B');
    pending.resolve(new Response('{}', { status }));
    await assert.rejects(request, { status: 401 });
    assert.equal(requests, 1);
    assert.equal(session.readSession().refreshToken, 'refresh-B');
  }
});

test('a failed refresh from the previous account leaves the new login intact', async () => {
  login('A');
  const pending = deferred();
  globalThis.fetch = async url => url.endsWith('/auth/refresh') ? pending.promise : new Response(null, { status: 401 });
  const request = api.apiFetch('/users/me');
  await new Promise(resolve => setImmediate(resolve));
  login('B');
  pending.resolve(new Response(null, { status: 401 }));
  await assert.rejects(request, { status: 401 });
  assert.equal(session.readSession().refreshToken, 'refresh-B');
});

test('a new account can refresh independently while the previous refresh is pending', async () => {
  login('A');
  const old = deferred();
  globalThis.fetch = async () => old.promise;
  const first = api.getOrQueueRefreshSession();
  login('B');
  globalThis.fetch = async () => rotation('B2');
  assert.equal((await api.getOrQueueRefreshSession()).refreshToken, 'refresh-B2');
  old.resolve(rotation('A2'));
  assert.equal(await first, null);
  assert.equal(session.readSession().refreshToken, 'refresh-B2');
});

test('Diagram/Review use the shared refresh and preserve project context', async () => {
  login('A');
  const projectId = 'd329250f-b994-4884-81bc-c43cfba43523';
  const calls = [];
  globalThis.fetch = async (url, init) => {
    calls.push({ url, init });
    if (url.endsWith('/auth/refresh')) return rotation('A2');
    if (calls.length === 1) return new Response(null, { status: 401 });
    return Response.json({ items: [], projectId });
  };
  await diagram.diagramApi.getDiagrams({ projectId });
  assert.equal(calls.length, 3);
  assert.equal(new URL(calls[2].url, 'http://test').searchParams.get('projectId'), projectId);
  await diagram.diagramApi.submitReview(projectId);
  assert.equal(JSON.parse(calls.at(-1).init.body).projectId, projectId);
  await diagram.diagramApi.exportZip(projectId);
  assert.equal(calls.at(-1).init.headers.get('Authorization'), `Bearer ${token('A2')}`);
});

test('expired access tokens refresh before sending and notify the active session subscriber', async () => {
  const original = login('expired');
  store.set('umlforge.session.v2', JSON.stringify({ ...original, expiresAt: 0 }));
  const updates = [];
  const unsubscribe = session.subscribeSession(value => updates.push(value));
  const calls = [];
  globalThis.fetch = async (url, init) => {
    calls.push({ url, init });
    return url.endsWith('/auth/refresh') ? rotation('fresh') : Response.json({ ok: true });
  };
  await api.apiFetch('/users/me');
  unsubscribe();
  assert.equal(calls.length, 2);
  assert.ok(calls[0].url.endsWith('/auth/refresh'));
  assert.equal(calls[1].init.headers.get('Authorization'), `Bearer ${token('fresh')}`);
  assert.equal(updates[0].refreshToken, 'refresh-fresh');
});

test('a hung refresh times out and releases the lock for a later login', async () => {
  login('A');
  const originalTimeout = AbortSignal.timeout;
  AbortSignal.timeout = milliseconds => {
    assert.equal(milliseconds, 15000);
    const controller = new AbortController();
    setTimeout(() => controller.abort(new DOMException('Timed out', 'TimeoutError')), 5);
    return controller.signal;
  };
  try {
    globalThis.fetch = async (url, init) => {
      if (!url.endsWith('/auth/refresh')) return new Response(null, { status: 401 });
      return new Promise((_, reject) => init.signal.addEventListener('abort', () => reject(init.signal.reason), { once: true }));
    };
    await assert.rejects(api.apiFetch('/users/me'), { status: 401 });
    assert.equal(session.readSession(), null);
    login('B');
    globalThis.fetch = async () => rotation('B2');
    assert.equal((await api.getOrQueueRefreshSession()).refreshToken, 'refresh-B2');
  } finally {
    AbortSignal.timeout = originalTimeout;
  }
});
