import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import ts from 'typescript';

function moduleUrl(code) {
  const js = ts.transpileModule(code, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
  return `data:text/javascript;base64,${Buffer.from(js).toString('base64')}`;
}
const source = relative => readFileSync(new URL(relative, import.meta.url), 'utf8');
const sessionUrl = moduleUrl(source('../src/auth/session.ts'));
const apiUrl = moduleUrl(source('../src/shared/api.ts').replace("'../auth/session'", JSON.stringify(sessionUrl)));
const { notificationLink } = await import(moduleUrl(source('../src/features/notification/notificationApi.ts')
  .replace("'../../shared/api'", JSON.stringify(apiUrl))));
const project = '/projects/08c28e1f-cfd5-4d3d-9dca-5bc9727fff52';

test('existing invitation notifications open My Invitations before project membership', () => {
  assert.equal(notificationLink(project, 'InvitationSent'), '/invitations/mine');
});

test('new invitation links open the same route as the workspace invitations button', () => {
  assert.equal(notificationLink('/invitations/mine'), '/invitations/mine');
  assert.equal(notificationLink('/invitations/mine', 'InvitationSent'), '/invitations/mine');
});

test('collaboration and render notifications retain their own destinations', () => {
  assert.equal(notificationLink(project, 'MemberAdded'), project);
  assert.equal(notificationLink(`${project}?tab=review`, 'ReviewSubmitted'), `${project}?tab=review`);
  assert.equal(notificationLink('/diagrams/08c28e1f-cfd5-4d3d-9dca-5bc9727fff52', 'RenderCompleted'), '/diagrams/08c28e1f-cfd5-4d3d-9dca-5bc9727fff52');
});

test('untrusted destinations remain blocked and invitation type always chooses a local route', () => {
  for (const link of ['https://example.com', '//example.com', 'javascript:alert(1)', '/invitations/mine?redirect=https://example.com']) {
    assert.equal(notificationLink(link), '/notifications');
    assert.equal(notificationLink(link, 'InvitationSent'), '/invitations/mine');
  }
});
