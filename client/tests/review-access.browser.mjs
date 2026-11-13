import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const client = fileURLToPath(new URL('../', import.meta.url));
const profile = mkdtempSync(join(tmpdir(), 'umlforge-review-access-'));
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const server = spawn(process.execPath, [join(client, 'node_modules/vite/bin/vite.js'), '--port', '5181'], { cwd: client, windowsHide: true, stdio: 'ignore' });
let browser, socket;
try {
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch('http://127.0.0.1:5181')).ok) break; } catch { /* starting */ }
    await pause(100);
  }
  browser = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--remote-debugging-port=9230', `--user-data-dir=${profile}`, 'about:blank',
  ], { windowsHide: true, stdio: 'ignore' });
  let tabs;
  for (let i = 0; i < 100; i++) {
    try { tabs = await (await fetch('http://127.0.0.1:9230/json')).json(); break; } catch { await pause(100); }
  }
  assert.ok(tabs?.length, 'Edge starts (override EDGE_PATH if needed)');
  socket = new WebSocket(tabs.find(tab => tab.type === 'page').webSocketDebuggerUrl);
  await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }));
  let sequence = 0;
  const pending = new Map();
  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (!message.id) return;
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(message.error.message)); else resolve(message.result);
  });
  const cdp = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++sequence; pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    return result.result.value;
  };
  const waitFor = async expression => {
    for (let i = 0; i < 100; i++) { if (await evaluate(`Boolean(${expression})`)) return; await pause(50); }
    throw Error('Timed out: ' + expression);
  };
  const A = '11111111-1111-4111-8111-111111111111';
  const B = '22222222-2222-4222-8222-222222222222';
  await cdp('Page.enable');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const scenario = JSON.parse(sessionStorage.getItem('test.scenario') ?? '{}');
    const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now()/1000) + 3600 }));
    sessionStorage.setItem('umlforge.session.v2', JSON.stringify({ accessToken: 'e30.' + payload + '.sig', refreshToken: 'test', expiresAt: Date.now()+3600000 }));
    window.__calls = []; window.__scenario = scenario;
    window.fetch = async (url, options = {}) => {
      const path = new URL(url, location.origin).pathname;
      window.__calls.push(path);
      if (path === '/api/users/me') return Response.json({ id: 'current-user', username: 'tester', fullName: 'Tester', role: scenario.globalRole ?? 'Student' });
      if (path.startsWith('/api/projects/')) {
        const id = path.split('/').pop();
        if (scenario.slow && id === '${A}') await new Promise(resolve => setTimeout(resolve, 600));
        options.signal?.throwIfAborted();
        if (scenario.fail) return Response.json({ detail: 'Không có quyền truy cập project.' }, { status: 403 });
        const role = scenario.slow ? (id === '${A}' ? 'Leader' : 'Member') : scenario.role;
        return Response.json({ id, members: role ? [{ userId: 'current-user', role }] : [] });
      }
      if (path === '/api/reviews') return Response.json({ items: [{ id: 'review-0001', projectId: '${A}', status: 'Submitted', submittedAt: new Date().toISOString() }], totalItems: 1, totalPages: 1, page: 1, pageSize: 20 });
      return Response.json({ items: [], totalItems: 0, totalPages: 0, page: 1, pageSize: 20, count: 0 });
    };
  })();` });
  await cdp('Page.navigate', { url: 'http://127.0.0.1:5181/login' });
  await waitFor('document.readyState === "complete"');
  async function open(scenario, path = `/reviews?projectId=${A}`) {
    await evaluate(`sessionStorage.setItem('test.scenario', ${JSON.stringify(JSON.stringify(scenario))})`);
    await cdp('Page.navigate', { url: 'http://127.0.0.1:5181' + path });
    await waitFor(`window.__scenario && !document.querySelector('[role="status"]') && (document.querySelector('.review-panel') || document.querySelector('[role="alert"]'))`);
  }
  const button = text => `[...document.querySelectorAll('button')].some(b => b.textContent.includes(${JSON.stringify(text)}))`;
  for (const [globalRole, role, submit, approve] of [
    ['Student', 'Leader', true, false], ['Student', 'Member', false, false],
    ['Lecturer', 'Lecturer', false, true], ['Lecturer', null, false, false], ['Admin', null, false, false],
  ]) {
    await open({ globalRole, role });
    assert.equal(await evaluate(button('Gửi duyệt')), submit, `${globalRole}/${role}: submit`);
    assert.equal(await evaluate(button('Phê duyệt (Approve)')), approve, `${globalRole}/${role}: approve`);
    if (!role) assert.equal(await evaluate(`window.__calls.includes('/api/reviews')`), false);
    console.log(`PASS ${globalRole}/${role ?? 'no membership'} uses project membership`);
  }
  await open({ fail: true });
  assert.equal(await evaluate(`window.__calls.includes('/api/reviews')`), false);
  assert.equal(await evaluate(button('Gửi duyệt')), false);
  console.log('PASS Workspace 403 does not load Review or expose actions');
  await open({ role: 'Leader' }, `/projects/${A}/reviews?projectId=${B}`);
  assert.equal(await evaluate(`window.__calls.includes('/api/projects/${A}')`), true);
  assert.equal(await evaluate(`window.__calls.includes('/api/projects/${B}')`), false);
  console.log('PASS project route takes precedence over query parameter');
  await evaluate(`sessionStorage.setItem('test.scenario', '{"slow":true}')`);
  await cdp('Page.navigate', { url: `http://127.0.0.1:5181/reviews?projectId=${A}` });
  await waitFor(`window.__scenario?.slow && document.querySelector('[role="status"]')`);
  assert.equal(await evaluate(button('Gửi duyệt')), false);
  await evaluate(`history.pushState({}, '', '/reviews?projectId=${B}'); dispatchEvent(new PopStateEvent('popstate'));`);
  await waitFor(`document.querySelector('.review-panel') !== null`);
  await pause(800);
  assert.equal(await evaluate(button('Gửi duyệt')), false);
  console.log('PASS loading hides actions; late Leader response for A cannot grant Leader in B');
} finally {
  socket?.close(); browser?.kill(); server.kill();
  await pause(500);
  // The directory comes directly from mkdtemp under the OS temp directory.
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Edge may still hold profile files */ }
}
