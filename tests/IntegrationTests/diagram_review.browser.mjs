// Uses only real requests to the nginx client; no fetch interception or fake JWT.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

if (!process.env.UMLFORGE_E2E_STATE) throw Error('Run verify_diagram_review.py to create the isolated real stack first.');
const state = JSON.parse(readFileSync(process.env.UMLFORGE_E2E_STATE, 'utf8'));
const profile = mkdtempSync(join(tmpdir(), 'umlforge-real-review-'));
const browser = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', [
  '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
  '--remote-debugging-port=9232', `--user-data-dir=${profile}`, 'about:blank',
], { windowsHide: true, stdio: 'ignore' });
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
let socket;
try {
  let tabs;
  for (let i = 0; i < 100; i++) {
    try { tabs = await (await fetch('http://127.0.0.1:9232/json')).json(); break; } catch { await pause(100); }
  }
  assert.ok(tabs?.length, 'Edge starts (override EDGE_PATH if needed)');
  socket = new WebSocket(tabs.find(tab => tab.type === 'page').webSocketDebuggerUrl);
  await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }));
  let sequence = 0;
  const pending = new Map();
  const requests = [];
  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) {
      const { resolve, reject } = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) reject(new Error(message.error.message)); else resolve(message.result);
    } else if (message.method === 'Network.requestWillBeSent') {
      const request = message.params.request;
      const url = new URL(request.url);
      if (url.pathname.startsWith('/api/')) requests.push({ path: url.pathname, query: url.search, method: request.method, body: request.postData });
    }
  });
  const cdp = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++sequence; pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    return result.result.value;
  };
  const waitFor = async (expression, description, seconds = 15) => {
    for (let i = 0; i < seconds * 10; i++) {
      if (await evaluate(`Boolean(${expression})`)) return;
      await pause(100);
    }
    throw Error('Timed out: ' + description);
  };
  const click = text => evaluate(`(() => {
    const button = [...document.querySelectorAll('button')].find(b => b.textContent.includes(${JSON.stringify(text)}));
    if (!button || button.disabled) throw Error('Button unavailable: ' + ${JSON.stringify(text)}); button.click();
  })()`);
  const setValue = (selector, value, element = 'HTMLInputElement') => evaluate(`(() => {
    const field = document.querySelector(${JSON.stringify(selector)});
    Object.getOwnPropertyDescriptor(${element}.prototype, 'value').set.call(field, ${JSON.stringify(value)});
    field.dispatchEvent(new Event('input', { bubbles: true }));
  })()`);
  await cdp('Page.enable');
  await cdp('Network.enable');
  let initialization;
  async function as(actor, path) {
    if (initialization) await cdp('Page.removeScriptToEvaluateOnNewDocument', { identifier: initialization });
    const login = state.actors[actor];
    const saved = { accessToken: login.accessToken, refreshToken: login.refreshToken, expiresAt: Date.now() + login.expiresIn * 1000 };
    const result = await cdp('Page.addScriptToEvaluateOnNewDocument', {
      source: `sessionStorage.setItem('umlforge.session.v2', ${JSON.stringify(JSON.stringify(saved))});`,
    });
    initialization = result.identifier;
    await cdp('Page.navigate', { url: state.clientUrl + path });
    await waitFor(`document.querySelector('.app-shell')`, 'real /users/me authenticated session');
  }

  await as('student1', `/projects/${state.projectA}`);
  await waitFor(`document.querySelector('h1')?.textContent === 'E2E Project A'`, 'real project A detail');
  await click('Sơ đồ UML');
  await waitFor(`document.querySelector('.diagram-list-panel')?.textContent.includes('A Leader UseCase')`, 'real diagrams in A');
  assert.equal(await evaluate(`document.querySelector('.diagram-list-panel').textContent.includes('B UseCase')`), false);
  await click('Tạo sơ đồ mới');
  await waitFor(`document.querySelector('dialog[open]')`, 'create dialog');
  await setValue('dialog input', 'A Browser UseCase');
  await setValue('dialog textarea', state.modelJson, 'HTMLTextAreaElement');
  await evaluate(`document.querySelector('dialog form').requestSubmit()`);
  await waitFor(`document.querySelector('h1')?.textContent === 'A Browser UseCase'`, 'real diagram creation');
  const diagramId = await evaluate(`location.pathname.split('/').pop()`);
  assert.equal(await evaluate(`new URLSearchParams(location.search).get('projectId')`), state.projectA);
  assert.ok(requests.some(request => request.path === '/api/diagrams' && request.method === 'POST'
    && JSON.parse(request.body).projectId === state.projectA));
  console.log('PASS real ProjectDetail A -> create -> diagram view preserves project GUID');
  await click('Render sơ đồ');
  await waitFor(`document.querySelector('.status-badge.status-success') && document.querySelector('.svg-canvas svg')`, 'real render completes and SVG appears', 90);
  assert.ok(requests.some(request => request.path === `/api/diagrams/${diagramId}/render` && request.method === 'POST'));
  console.log('PASS browser render reaches real gRPC/PlantUML and polling displays SVG');

  await as('student2', `/reviews?projectId=${state.projectA}`);
  await waitFor(`document.querySelector('.review-panel')`, 'member review history');
  assert.equal(await evaluate(`[...document.querySelectorAll('button')].some(b => b.textContent.includes('Gửi duyệt'))`), false);
  console.log('PASS real Member sees reviews without Leader submit action');

  await as('student1', `/reviews?projectId=${state.projectA}`);
  await waitFor(`document.querySelector('.review-panel') && !document.querySelector('.review-panel .skeleton')`, 'leader review history');
  await click('Gửi duyệt');
  await waitFor(`document.querySelector('.toast--success')?.textContent.includes('Đã gửi toàn bộ sơ đồ')`, 'real review submission');
  assert.ok(requests.some(request => request.path === '/api/reviews' && request.method === 'POST' && JSON.parse(request.body).projectId === state.projectA));
  console.log('PASS real Leader submits project A after all actual renders completed');

  await as('lecturer1', `/diagrams/${diagramId}?projectId=${state.projectA}`);
  await waitFor(`document.querySelector('h1')?.textContent === 'A Browser UseCase'`, 'lecturer reads real diagram');
  assert.equal(await evaluate(`[...document.querySelectorAll('button')].some(b => b.textContent.includes('Render sơ đồ'))`), false);
  await setValue('form textarea', 'Browser lecturer feedback on real diagram.', 'HTMLTextAreaElement');
  await click('Gửi góp ý');
  await waitFor(`document.querySelector('.comment-list')?.textContent.includes('Browser lecturer feedback on real diagram.')`, 'real lecturer comment saved');
  console.log('PASS real Lecturer reads/comments while render action is hidden');

  await as('lecturer2', `/reviews?projectId=${state.projectA}`);
  await waitFor(`document.querySelector('[role="alert"]')`, 'unassigned lecturer access denied');
  assert.equal(await evaluate(`document.querySelector('.review-panel') !== null`), false);
  console.log('PASS real unassigned Lecturer cannot enter project A Review');

  await as('lecturer1', `/reviews?projectId=${state.projectA}`);
  await waitFor(`document.querySelector('.review-panel') && !document.querySelector('.review-panel .skeleton')`, 'assigned lecturer reviews');
  await click('Phê duyệt (Approve)');
  await waitFor(`document.querySelector('.toast--success')?.textContent.includes('Đã phê duyệt')`, 'real lecturer approval');
  await waitFor(`![...document.querySelectorAll('button')].some(b => b.textContent.includes('Phê duyệt (Approve)'))`, 'approved review no longer has decide action');
  console.log('PASS real assigned Lecturer approves browser-created review');

  await as('student3', `/projects/${state.projectB}`);
  await waitFor(`document.querySelector('h1')?.textContent === 'E2E Project B'`, 'real project B detail');
  await click('Sơ đồ UML');
  await waitFor(`document.querySelector('.diagram-list-panel')?.textContent.includes('B UseCase')`, 'real B diagrams');
  assert.equal(await evaluate(`document.querySelector('.diagram-list-panel').textContent.includes('A Browser UseCase')`), false);
  assert.equal(await evaluate(`document.querySelector('.diagram-list-panel').textContent.includes('A Leader UseCase')`), false);
  assert.ok(requests.every(request => !request.path.startsWith('/api/dev/')));
  console.log('PASS real project B stays isolated; no dev endpoints, network mocks or fake engine used');
} finally {
  socket?.close(); browser.kill();
  await pause(500);
  assert.equal(resolve(profile, '..'), resolve(tmpdir()));
  assert.ok(profile.split(/[\\/]/).pop().startsWith('umlforge-real-review-'));
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Edge may still hold profile files */ }
}
