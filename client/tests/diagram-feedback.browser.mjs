import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const client = fileURLToPath(new URL('../', import.meta.url));
const profile = mkdtempSync(join(tmpdir(), 'umlforge-diagram-ui-'));
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const server = spawn(process.execPath, [join(client, 'node_modules/vite/bin/vite.js'), '--port', '5182'], { cwd: client, windowsHide: true, stdio: 'ignore' });
let browser, socket;
try {
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch('http://127.0.0.1:5182')).ok) break; } catch { /* starting */ }
    await pause(100);
  }
  browser = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--remote-debugging-port=9231', `--user-data-dir=${profile}`, 'about:blank',
  ], { windowsHide: true, stdio: 'ignore' });
  let tabs;
  for (let i = 0; i < 100; i++) {
    try { tabs = await (await fetch('http://127.0.0.1:9231/json')).json(); break; } catch { await pause(100); }
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
  await cdp('Page.enable');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now()/1000) + 3600 }));
    sessionStorage.setItem('umlforge.session.v2', JSON.stringify({ accessToken: 'e30.' + payload + '.sig', refreshToken: 'test', expiresAt: Date.now()+3600000 }));
    window.__calls = []; window.__alerts = 0; window.__createMode = 'ok';
    const lecturer = sessionStorage.getItem('test.uiLecturer') === 'true';
    window.alert = () => window.__alerts++;
    window.confirm = () => true;
    const summary = { id: 'diagram-1', projectId: '${A}', type: 2, title: 'Diagram UI', modelJson: '{}', status: 'Draft', currentVersion: 1, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() };
    const paged = items => ({ items, totalItems: items.length, totalPages: 1, page: 1, pageSize: 20 });
    window.fetch = async (url, options = {}) => {
      const path = new URL(url, location.origin).pathname;
      window.__calls.push({ path, method: options.method ?? 'GET', body: options.body });
      if (path === '/api/users/me') return Response.json({ id: 'current-user', username: 'tester', fullName: 'Tester', role: lecturer ? 'Lecturer' : 'Student' });
      if (path.startsWith('/api/projects/')) return Response.json({ id: '${A}', name: 'UI project', rowVersion: 'v1', createdAt: new Date().toISOString(), members: [{ userId: 'current-user', role: lecturer ? 'Lecturer' : 'Leader' }] });
      if (path === '/api/diagrams' && options.method === 'POST') {
        if (window.__createMode === 'fail') return Response.json({ detail: 'Tiêu đề sơ đồ bị trùng.' }, { status: 409 });
        if (window.__createMode === 'slow') await new Promise(resolve => { window.__releaseCreate = resolve; });
        return Response.json(summary, { status: 201 });
      }
      if (path.endsWith('/render')) return window.__renderError
        ? Response.json({ detail: 'Sơ đồ đang được render.' }, { status: 409 }) : Response.json({ status: 'Pending' });
      if (path === '/api/diagrams/diagram-1') return Response.json(summary);
      if (path === '/api/diagrams') return Response.json(paged([summary]));
      if (path.endsWith('/comments')) return Response.json([]);
      if (path === '/api/reviews' && options.method === 'POST') return Response.json({ id: 'review-1' }, { status: 201 });
      if (path === '/api/reviews' && lecturer) return Response.json(paged([{ id: 'review-1', projectId: '${A}', status: 'Submitted', submittedAt: new Date().toISOString() }]));
      return Response.json(paged([]));
    };
  })();` });
  const click = text => evaluate(`(() => {
    const button = [...document.querySelectorAll('button')].find(b => b.textContent.includes(${JSON.stringify(text)}));
    if (!button || button.disabled) throw Error('Button unavailable'); button.click();
  })()`);
  const key = async (key, code, modifiers = 0) => {
    const windowsVirtualKeyCode = key === 'Escape' ? 27 : 9;
    await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key, code, modifiers, windowsVirtualKeyCode });
    await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key, code, modifiers, windowsVirtualKeyCode });
  };
  const setModel = value => evaluate(`(() => {
    const field = document.querySelector('dialog textarea');
    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set.call(field, ${JSON.stringify(value)});
    field.dispatchEvent(new Event('input', { bubbles: true }));
  })()`);
  const createCount = () => evaluate(`window.__calls.filter(c => c.path === '/api/diagrams' && c.method === 'POST').length`);
  const openList = async () => {
    await cdp('Page.navigate', { url: `http://127.0.0.1:5182/diagrams?projectId=${A}` });
    await waitFor(`document.querySelector('.diagram-list-panel tbody')`);
  };
  await openList();
  await click('Tạo sơ đồ mới');
  await waitFor(`document.querySelector('dialog[open]')`);
  assert.equal(await evaluate(`document.getElementById(document.querySelector('dialog').getAttribute('aria-labelledby')).textContent`), 'Tạo sơ đồ UML mới');
  assert.equal(await evaluate(`document.activeElement === document.querySelector('dialog input')`), true);
  assert.equal(await evaluate(`[...document.querySelectorAll('dialog input, dialog select, dialog textarea')].every(field => field.labels.length === 1)`), true);
  await evaluate(`document.querySelector('dialog button[type="submit"]').focus()`);
  await key('Tab', 'Tab');
  assert.equal(await evaluate(`document.querySelector('dialog').contains(document.activeElement)`), true);
  await evaluate(`document.querySelector('dialog button').focus()`);
  await key('Tab', 'Tab', 8);
  assert.equal(await evaluate(`document.querySelector('dialog').contains(document.activeElement)`), true);
  await key('Escape', 'Escape');
  await waitFor(`!document.querySelector('dialog')`);
  assert.equal(await evaluate(`document.activeElement.textContent.includes('Tạo sơ đồ mới')`), true);
  console.log('PASS create dialog has name/labels, initial focus, Tab/Shift+Tab containment, Esc and focus restoration');

  await click('Tạo sơ đồ mới');
  await setModel('{ invalid');
  await evaluate(`document.querySelector('dialog form').requestSubmit()`);
  await waitFor(`document.querySelector('dialog [role="alert"]')`);
  assert.equal(await createCount(), 0);
  assert.equal(await evaluate(`document.querySelector('dialog textarea').getAttribute('aria-invalid')`), 'true');
  assert.equal(await evaluate(`document.querySelector('dialog textarea').getAttribute('aria-describedby') === document.querySelector('dialog [role="alert"]').id`), true);
  assert.equal(await evaluate(`document.activeElement === document.querySelector('dialog [role="alert"]')`), true);
  console.log('PASS invalid JSON stays in dialog, links/focuses error, sends no request');

  await setModel('{}');
  await evaluate(`window.__createMode = 'fail'; document.querySelector('dialog form').requestSubmit()`);
  await waitFor(`document.querySelector('dialog [role="alert"]')?.textContent.includes('Tiêu đề sơ đồ bị trùng')`);
  assert.equal(await evaluate(`document.querySelector('dialog button[type="submit"]').disabled`), false);
  console.log('PASS API error remains accessible in dialog and allows correction/retry');

  const before = await createCount();
  await evaluate(`window.__createMode = 'slow'; document.querySelector('dialog form').requestSubmit(); document.querySelector('dialog form').requestSubmit();`);
  await waitFor(`window.__releaseCreate`);
  assert.equal(await createCount(), before + 1);
  assert.equal(await evaluate(`document.querySelector('dialog form').getAttribute('aria-busy')`), 'true');
  assert.equal(await evaluate(`[...document.querySelectorAll('dialog button')].every(button => button.disabled || button.closest('fieldset')?.disabled)`), true);
  await key('Escape', 'Escape');
  assert.equal(await evaluate(`document.querySelector('dialog').open`), true);
  await evaluate('window.__releaseCreate()');
  await waitFor(`location.pathname === '/diagrams/diagram-1' && document.querySelector('.toast--success')`);
  assert.equal(await evaluate(`document.querySelector('.toast-region').getAttribute('aria-live')`), 'polite');
  console.log('PASS pending submit blocks duplicate requests/Esc; success toast survives navigation');
  await waitFor(`!document.querySelector('.toast')`);
  console.log('PASS toast auto-dismisses after four seconds');

  await openList();
  await click('Render');
  await waitFor(`document.querySelector('.toast--success')?.textContent.includes('Đã gửi yêu cầu render')`);
  await evaluate(`window.__renderError = true`);
  await click('Render');
  await waitFor(`document.querySelector('.toast--error')?.textContent.includes('Sơ đồ đang được render')`);
  await evaluate(`document.querySelector('.toast-close').click()`);
  assert.equal(await evaluate(`document.querySelector('.toast') === null`), true);
  assert.equal(await evaluate('window.__alerts'), 0);
  console.log('PASS render success/error use dismissible toast without native alerts');

  await cdp('Page.navigate', { url: `http://127.0.0.1:5182/projects/${A}` });
  await waitFor(`document.querySelector('h1')?.textContent === 'UI project'`);
  await click('Sơ đồ UML');
  await waitFor(`document.querySelector('.diagram-list-panel tbody')`);
  await click('Tạo sơ đồ mới');
  await waitFor(`document.querySelector('dialog[open]')`);
  await key('Escape', 'Escape');
  await waitFor(`!document.querySelector('dialog')`);
  assert.equal(await evaluate(`document.activeElement.textContent.includes('Tạo sơ đồ mới')`), true);
  console.log('PASS ProjectDetail uses the same accessible create dialog');

  await cdp('Page.navigate', { url: `http://127.0.0.1:5182/reviews?projectId=${A}` });
  await waitFor(`document.querySelector('.review-panel') && !document.querySelector('.review-panel .skeleton')`);
  await click('Gửi duyệt');
  await waitFor(`document.querySelector('.toast--success')?.textContent.includes('Đã gửi toàn bộ sơ đồ')`);
  assert.equal(await evaluate('window.__alerts'), 0);
  console.log('PASS Review submission uses toast without native alerts');
  await evaluate(`sessionStorage.setItem('test.uiLecturer', 'true')`);
  await cdp('Page.navigate', { url: `http://127.0.0.1:5182/reviews?projectId=${A}` });
  await waitFor(`document.querySelector('.review-panel textarea')`);
  await click('Yêu cầu sửa đổi');
  await waitFor(`document.querySelector('.review-panel [role="alert"]')`);
  assert.equal(await evaluate(`document.activeElement === document.querySelector('.review-panel textarea')`), true);
  assert.equal(await evaluate(`document.querySelector('.review-panel textarea').getAttribute('aria-describedby') === document.querySelector('.review-panel [role="alert"]').id`), true);
  assert.equal(await evaluate(`window.__calls.some(c => c.path.endsWith('/request-changes'))`), false);
  assert.equal(await evaluate('window.__alerts'), 0);
  console.log('PASS empty lecturer feedback focuses labelled field, links inline error and sends no request');
} finally {
  socket?.close(); browser?.kill(); server.kill();
  await pause(500);
  // mkdtemp creates only a new task-owned directory under the OS temp directory.
  assert.equal(join(tmpdir(), profile.split(/[\\/]/).pop()), profile);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Edge may still hold profile files */ }
}
