import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import ts from 'typescript';

const source = readFileSync(new URL('../src/features/diagram/diagramNavigation.ts', import.meta.url), 'utf8');
const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const { diagramListLink } = await import(`data:text/javascript;base64,${Buffer.from(js).toString('base64')}`);
const projectId = '08c28e1f-cfd5-4d3d-9dca-5bc9727fff52';
const project = `/projects/${projectId}`;

test('a diagram opened directly returns to its project diagram tab', () => {
  assert.equal(diagramListLink(projectId), `${project}?tab=diagrams`);
  assert.equal(diagramListLink(undefined), '/projects');
});

test('embedded diagram list retains tab, search, filters and pagination', () => {
  const origin = `${project}?tab=diagrams&diagramSearch=UML&diagramType=2&diagramStatus=Rendered&diagramPage=2`;
  assert.equal(diagramListLink(projectId, origin), origin);
});

test('standalone diagram lists retain their own route', () => {
  const origin = `${project}/diagrams?diagramPage=2`;
  assert.equal(diagramListLink(projectId, origin), origin);
  const legacy = `/diagrams?projectId=${projectId}&diagramSearch=UML`;
  assert.equal(diagramListLink(projectId, legacy), legacy);
});

test('another project, external URLs and unrelated screens cannot become the return destination', () => {
  for (const origin of ['https://example.com', '//example.com', 'javascript:alert(1)', '/projects/another-project?tab=diagrams', '/diagrams?projectId=another-project', '/admin/users', {}]) {
    assert.equal(diagramListLink(projectId, origin), `${project}?tab=diagrams`);
  }
});
