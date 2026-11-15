"""Real Docker/HTTP/Streams/gRPC/PlantUML acceptance; Python stdlib only.

Run from the repository root; see README.md in this directory for source worktrees.
No fake engine, injected member access, database writes or manually published events.
"""
import argparse
import io
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import zipfile


class Acceptance:
    def __init__(self, args):
        self.repo = Path(__file__).resolve().parents[2]
        self.output = Path(args.output).resolve()
        self.output.mkdir(parents=True, exist_ok=True)
        self.run_id = secrets.token_hex(4)
        self.network = f'umlforge-e2e-{self.run_id}'
        self.containers = {}
        self.checks = []
        self.status = 'failed'
        self.error = None
        self.secrets = []
        self.sources = {
            'identity': Path(args.identity_source).resolve(),
            'workspace': Path(args.workspace_source).resolve(),
            'gateway': Path(args.gateway_source).resolve(),
            **{name: self.repo for name in ['diagram', 'engine', 'render', 'notification', 'client']},
        }
        self.variables = {
            'MSSQL_SA_PASSWORD': 'Verify!' + secrets.token_hex(16),
            'JWT_KEY': secrets.token_hex(32),
            'JWT_ISSUER': 'umlforge', 'JWT_AUDIENCE': 'umlforge-client',
            'REDIS_URL': 'redis:6379', 'ASPNETCORE_ENVIRONMENT': 'Development',
            'DIAGRAM_ENGINE_URL': 'http://diagram-engine:8080',
            'PLANTUML_URL': 'http://plantuml:8080',
        }
        self.password = 'Verify!' + secrets.token_hex(12)
        self.secrets += [self.password, self.variables['MSSQL_SA_PASSWORD'], self.variables['JWT_KEY']]
        self.manifest = {name: subprocess.check_output(['git', '-C', str(path), 'rev-parse', 'HEAD'], text=True).strip()
                         for name, path in self.sources.items()}

    def redact(self, value):
        for secret in self.secrets:
            value = value.replace(secret, '[redacted]')
        return value

    def docker(self, *args, env=None, check=True):
        result = subprocess.run(['docker', *args], capture_output=True, text=True, encoding='utf8', errors='replace', env=env)
        if check and result.returncode:
            raise RuntimeError(self.redact(result.stderr[-1500:]))
        return result.stdout.strip()

    def passed(self, label):
        self.checks.append(label)
        print('PASS ' + label, flush=True)

    def check(self, condition, label):
        if not condition:
            raise AssertionError(label)
        self.passed(label)

    def until(self, predicate, label, seconds=60):
        deadline = time.monotonic() + seconds
        last = None
        while time.monotonic() < deadline:
            try:
                value = predicate()
                if value:
                    self.passed(label)
                    return value
            except (urllib.error.URLError, TimeoutError, OSError, RuntimeError) as error:
                last = type(error).__name__
            time.sleep(.5)
        raise AssertionError(f'{label}: timed out ({last or "condition not met"})')

    def build(self):
        dockerfiles = {
            'identity': 'Services/Identity/Identity.API/Dockerfile',
            'workspace': 'Services/Workspace/Workspace.API/Dockerfile',
            'gateway': 'Gateway/UMLForge.Gateway/Dockerfile',
            'diagram': 'Services/Diagram/Diagram.API/Dockerfile',
            'engine': 'Services/DiagramEngine/DiagramEngine.Grpc/Dockerfile',
            'render': 'Services/Render/Render.Worker/Dockerfile',
            'notification': 'Services/Notification/Notification.API/Dockerfile',
            'client': 'Dockerfile',
        }
        # Docker Desktop context metadata can be locked by concurrent buildx on Windows.
        for name, source in self.sources.items():
            context = source / ('client' if name == 'client' else 'src')
            command = ['docker', 'build', '--progress', 'plain', '--build-arg', f'REVISION={self.manifest[name]}',
                       '--label', f'org.opencontainers.image.revision={self.manifest[name]}',
                       '-f', str(context / dockerfiles[name]), '-t', f'umlforge-e2e-{name}:local', str(context)]
            with (self.output / f'build-{name}.log').open('w', encoding='utf8') as log:
                result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT)
            if result.returncode:
                raise RuntimeError(f'{name} image build failed; see build-{name}.log')
            self.passed(f'build {name} at {self.manifest[name][:8]}')

    def config(self, name):
        file = self.sources[name] / f'deploy/compose/{name}.yml'
        text = file.read_text(encoding='utf8')
        block = text.split('    environment:\n', 1)[1].split('    ports:', 1)[0]
        values = {}
        for line in block.splitlines():
            match = re.match(r'      ([\w_\-]+): (.*)', line)
            if match:
                value = match[2].split(' #', 1)[0].strip().strip('"')
                values[match[1]] = re.sub(r'\$\{(\w+)(?::[-?][^}]*)?\}', lambda m: self.variables[m[1]], value)
        return values

    def check_image_revisions(self):
        for name, revision in self.manifest.items():
            actual = self.docker('image', 'inspect', '--format', '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                                 f'umlforge-e2e-{name}:local')
            if actual != revision:
                raise RuntimeError(f'{name} image revision differs from source; rerun without --skip-build')

    def start(self, name, image, config=None, alias=None, internal_port=8080):
        container = f'umlforge-e2e-{self.run_id}-{name}'
        env = os.environ.copy()
        command = ['run', '-d', '--name', container, '--network', self.network, '--network-alias', alias or name]
        if internal_port:
            command += ['-p', f'127.0.0.1::{internal_port}']
        for key, value in (config or {}).items():
            env[key] = value
            command += ['-e', key]
        self.docker(*command, image, env=env)
        self.containers[name] = container

    def base(self, name, port=8080):
        mapping = self.docker('port', self.containers[name], f'{port}/tcp')
        return 'http://127.0.0.1:' + mapping.rsplit(':', 1)[1]

    def http(self, path, actor=None, method='GET', body=None, expected=200, code=None, headers=None, base=None, record=True):
        request_headers = {'Content-Type': 'application/json', **(headers or {})}
        if actor:
            request_headers['Authorization'] = 'Bearer ' + self.actors[actor]['accessToken']
        request = urllib.request.Request((base or self.gateway) + path, method=method, headers=request_headers,
                                         data=None if body is None else json.dumps(body).encode())
        try:
            response = urllib.request.urlopen(request, timeout=10)
        except urllib.error.HTTPError as error:
            response = error
        raw = response.read()
        content_type = response.headers.get('Content-Type', '')
        data = json.loads(raw) if raw and 'json' in content_type else raw
        if expected is not None:
            actual_code = data.get('code') if isinstance(data, dict) else None
            if response.code != expected or (code and actual_code != code):
                raise AssertionError(f'{method} {path}: expected {expected}/{code}, got {response.code}/{actual_code}')
            if record:
                self.passed(f'{method} {path} [{actor or "anonymous"}] -> {expected}' + (f'/{code}' if code else ''))
        return response.code, data, response.headers

    def healthy(self, name):
        return self.http('/health', base=self.base(name), expected=None, record=False)[0] == 200

    def prepare(self):
        self.docker('network', 'create', self.network)
        self.start('sqlserver', 'mcr.microsoft.com/mssql/server:2022-latest',
                   {'ACCEPT_EULA': 'Y', 'MSSQL_SA_PASSWORD': self.variables['MSSQL_SA_PASSWORD']}, internal_port=None)
        self.start('redis', 'redis:7-alpine', internal_port=None)
        self.start('plantuml', 'plantuml/plantuml-server:jetty')
        def sql_ready():
            env = os.environ.copy()
            env['SQLCMDPASSWORD'] = self.variables['MSSQL_SA_PASSWORD']
            return '1' in self.docker('exec', '-e', 'SQLCMDPASSWORD', self.containers['sqlserver'],
                                      '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', 'sa', '-C', '-b', '-Q', 'SELECT 1', env=env)
        self.until(sql_ready, 'SQL Server ready', 90)
        self.start('engine', 'umlforge-e2e-engine:local', {'ASPNETCORE_ENVIRONMENT': 'Development'}, alias='diagram-engine', internal_port=None)
        self.until(lambda: subprocess.run(['docker', 'exec', self.containers['engine'], 'dotnet', 'DiagramEngine.Grpc.dll', '--healthcheck'],
                                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0, 'real gRPC engine ready')
        for name in ['workspace', 'diagram', 'render', 'notification', 'identity', 'gateway']:
            config = self.config(name)
            if name == 'identity':
                config['Seed__DefaultPassword'] = self.password
            if name in ['diagram', 'render']:
                assert config['DiagramEngine__UseFake'] == 'false'
            # Keep seed startup output small; never enable sensitive data logging.
            config['Logging__LogLevel__Default'] = 'Information'
            config['Logging__LogLevel__Microsoft.EntityFrameworkCore'] = 'Warning'
            self.start(name, f'umlforge-e2e-{name}:local', config)
            self.until(lambda n=name: self.healthy(n), f'{name} healthy', 90)
        self.gateway = self.base('gateway')
        self.start('client', 'umlforge-e2e-client:local', internal_port=80)
        self.client = self.base('client', 80)
        self.until(lambda: self.http('/', base=self.client, expected=None, record=False)[0] == 200, 'nginx client ready')

    def access_ready(self, project_id, actor, allowed=True):
        return self.http(f'/api/diagrams?projectId={project_id}', actor, expected=None, record=False)[0] == (200 if allowed else 403)

    def setup_projects(self):
        self.actors = {}
        for name in ['admin', 'student1', 'student2', 'student3', 'lecturer1', 'lecturer2']:
            _, login, _ = self.http('/api/auth/login', method='POST', body={'username': name, 'password': self.password})
            self.actors[name] = login
            self.secrets += [login['accessToken'], login['refreshToken']]
        self.until(lambda: len(self.http('/api/directory/users?role=Lecturer', 'student1', record=False)[1]['items']) >= 2,
                   'Identity -> Outbox -> Redis Streams -> Workspace directory synchronized')
        self.A = self.http('/api/projects', 'student1', 'POST', {'name': 'E2E Project A'}, 201)[1]['id']
        self.B = self.http('/api/projects', 'student3', 'POST', {'name': 'E2E Project B'}, 201)[1]['id']
        for project, actor in [(self.A, 'student1'), (self.B, 'student3')]:
            self.until(lambda p=project, a=actor: self.access_ready(p, a), f'ProjectCreated gives {actor} real Diagram membership')
        self.http(f'/api/diagrams?projectId={self.A}', 'student3', expected=403, code='BR-11')
        invitation = self.http(f'/api/projects/{self.A}/invitations', 'student1', 'POST', {'username': 'student2'}, 201)[1]
        self.http(f'/api/invitations/{invitation["id"]}/accept', 'student2', 'POST', expected=200)
        self.until(lambda: self.access_ready(self.A, 'student2'), 'MemberAdded propagates real member access')
        for project, lecturer in [(self.A, 'lecturer1'), (self.B, 'lecturer2')]:
            self.http(f'/api/projects/{project}/lecturer', 'student1' if project == self.A else 'student3', 'PUT',
                      {'lecturerId': self.actors[lecturer]['user']['id']})
            self.until(lambda p=project, a=lecturer: self.access_ready(p, a), f'LecturerAssigned gives {lecturer} access only to assigned project')

    def model(self):
        return json.dumps({'title': 'Ordering', 'systemBoundary': 'CampusEats', 'actors': ['Student'], 'useCases': ['Order Food'],
                           'relations': [{'from': 'Student', 'to': 'Order Food', 'type': 'association'}]})

    def create(self, project, actor, title):
        return self.http('/api/diagrams', actor, 'POST', {'projectId': project, 'type': 2, 'title': title, 'modelJson': self.model()}, 201)[1]

    def rendered(self, diagram_id, actor):
        status, diagram, _ = self.http(f'/api/diagrams/{diagram_id}', actor, expected=None, record=False)
        if status == 200 and diagram['status'] == 'Failed':
            raise AssertionError('real render failed: ' + str(diagram.get('lastError')))
        return diagram if status == 200 and diagram['status'] == 'Rendered' else None

    def core_checks(self):
        self.d1 = self.create(self.A, 'student1', 'A Leader UseCase')
        self.d2 = self.create(self.A, 'student2', 'A Member UseCase')
        self.db = self.create(self.B, 'student3', 'B UseCase')
        self.http('/api/reviews', 'student1', 'POST', {'projectId': self.A}, 400, 'BR-29')
        self.http('/api/reviews', 'student2', 'POST', {'projectId': self.A}, 403, 'BR-29')
        self.http('/api/reviews', 'lecturer1', 'POST', {'projectId': self.A}, 403, 'BR-29')
        for path, method, body in [
            (f'/api/diagrams/{self.d1["id"]}', 'GET', None),
            ('/api/diagrams', 'POST', {'projectId': self.A, 'type': 2, 'title': 'Denied', 'modelJson': self.model()}),
            (f'/api/diagrams/{self.d1["id"]}/render', 'POST', None),
            (f'/api/reviews?projectId={self.A}', 'GET', None),
            (f'/api/diagrams/{self.d1["id"]}/comments', 'POST', {'content': 'Denied'}),
        ]:
            self.http(path, 'student3', method, body, 403, 'BR-11')
        self.http(f'/api/diagrams/{self.d1["id"]}', 'lecturer1')
        self.http(f'/api/diagrams/{self.d1["id"]}/comments', 'lecturer1', 'POST', {'content': 'Real lecturer feedback'}, 201)
        _, _, headers = self.http(f'/api/diagrams/{self.d1["id"]}', 'student1')
        for path, method, body in [
            ('/api/diagrams', 'POST', {'projectId': self.A, 'type': 2, 'title': 'Denied', 'modelJson': self.model()}),
            (f'/api/diagrams/{self.d1["id"]}', 'PUT', {'title': 'Denied', 'modelJson': self.model()}),
            (f'/api/diagrams/{self.d1["id"]}', 'DELETE', None),
            (f'/api/diagrams/{self.d1["id"]}/render', 'POST', None),
            (f'/api/diagrams/{self.d1["id"]}/versions/1/restore', 'POST', None),
        ]:
            self.http(path, 'lecturer1', method, body, 403, 'BR-32', headers={'If-Match': headers['ETag']})
        self.http(f'/api/diagrams/{self.d1["id"]}/render', 'student1', 'POST', expected=202)
        first = self.until(lambda: self.rendered(self.d1['id'], 'student1'), 'A1 rendered through real Streams, gRPC and PlantUML', 90)
        self.check('<svg' in first['svgXml'] and '@startuml' in first['plantUml'], 'real SVG and PlantUML artifacts returned')
        _, problem, _ = self.http('/api/reviews', 'student1', 'POST', {'projectId': self.A}, 400, 'BR-29')
        self.check(any(self.d2['id'] in item for item in problem['errors']['unrenderedDiagrams']), 'BR-29 still rejects project with a second unrendered diagram')
        self.http(f'/api/diagrams/{self.d2["id"]}/render', 'student2', 'POST', expected=202)
        self.until(lambda: self.rendered(self.d2['id'], 'student2'), 'A2 member render completed', 90)
        review = self.http('/api/reviews', 'student1', 'POST', {'projectId': self.A}, 201)[1]
        self.check({item['diagramId'] for item in review['diagrams']} == {self.d1['id'], self.d2['id']}, 'Review snapshot contains only project A diagrams')
        for actor, code in [('student1', 'BR-30'), ('student2', 'BR-30'), ('lecturer2', 'BR-11')]:
            self.http(f'/api/reviews/{review["id"]}/approve', actor, 'POST', {'note': 'Denied'}, 403, code)
        changes = self.http(f'/api/reviews/{review["id"]}/request-changes', 'lecturer1', 'POST', {'note': 'Please review actor names.'})[1]
        self.check(changes['status'] == 'ChangesRequested', 'assigned lecturer requests changes')
        review = self.http('/api/reviews', 'student1', 'POST', {'projectId': self.A}, 201)[1]
        approved = self.http(f'/api/reviews/{review["id"]}/approve', 'lecturer1', 'POST', {'note': 'Approved after review.'})[1]
        self.check(approved['status'] == 'Approved', 'only assigned lecturer approves real review')
        zip_data = self.http(f'/api/diagrams/export?projectId={self.A}', 'student1')[1]
        with zipfile.ZipFile(io.BytesIO(zip_data)) as archive:
            names = archive.namelist()
            self.check(any(name.endswith('.svg') for name in names) and any(name.endswith('.puml') for name in names)
                       and any(name.endswith('.json') for name in names), 'authenticated ZIP contains real SVG/PlantUML/JSON artifacts')
        listed = self.http(f'/api/diagrams?projectId={self.B}', 'student3')[1]['items']
        self.check({item['id'] for item in listed} == {self.db['id']}, 'project B list excludes all project A diagrams')

    def browser(self):
        state = {'actors': self.actors, 'projectA': self.A, 'projectB': self.B, 'clientUrl': self.client,
                 'diagramA': self.d1['id'], 'modelJson': self.model()}
        with tempfile.TemporaryDirectory(prefix='umlforge-e2e-state-') as temporary:
            state_path = Path(temporary) / 'state.json'
            state_path.write_text(json.dumps(state), encoding='utf8')
            env = os.environ.copy()
            env['UMLFORGE_E2E_STATE'] = str(state_path)
            result = subprocess.run(['node', str(Path(__file__).with_name('diagram_review.browser.mjs'))], env=env,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf8', errors='replace')
            (self.output / 'browser.log').write_text(self.redact(result.stdout), encoding='utf8')
            print(self.redact(result.stdout), flush=True)
            if result.returncode:
                raise AssertionError('real browser E2E failed; see browser.log')
            self.passed('real nginx -> Gateway -> service browser workflow')

    def revoke_checks(self):
        self.http(f'/api/projects/{self.A}/members/{self.actors["student2"]["user"]["id"]}', 'student1', 'DELETE', expected=204)
        self.until(lambda: self.access_ready(self.A, 'student2', False), 'MemberRemoved revokes Diagram access via real event')
        self.http(f'/api/diagrams/{self.d1["id"]}', 'student2', expected=403, code='BR-11')
        self.http(f'/api/projects/{self.A}/lecturer', 'student1', 'PUT', {'lecturerId': self.actors['lecturer2']['user']['id']})
        self.until(lambda: self.access_ready(self.A, 'lecturer2') and self.access_ready(self.A, 'lecturer1', False),
                   'LecturerAssigned revokes old lecturer and enables new lecturer through real event')

    def cleanup(self):
        for name, container in reversed(list(self.containers.items())):
            result = subprocess.run(['docker', 'logs', container], capture_output=True, text=True, encoding='utf8', errors='replace')
            (self.output / f'container-{name}.log').write_text(self.redact(result.stdout + result.stderr), encoding='utf8')
            self.docker('rm', '-f', container, check=False)
        self.docker('network', 'rm', self.network, check=False)
        (self.output / 'result.json').write_text(json.dumps({'status': self.status, 'error': self.error,
                                                          'sourceRevisions': self.manifest, 'passedChecks': self.checks}, indent=2), encoding='utf8')


def main():
    repo = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--identity-source', default=str(repo))
    parser.add_argument('--workspace-source', default=str(repo))
    parser.add_argument('--gateway-source', default=str(repo))
    parser.add_argument('--output', required=True)
    parser.add_argument('--skip-build', action='store_true', help='Use the images from a previous build of the same source.')
    args = parser.parse_args()
    acceptance = Acceptance(args)
    try:
        if not args.skip_build:
            acceptance.build()
        else:
            acceptance.check_image_revisions()
        acceptance.prepare()
        acceptance.setup_projects()
        acceptance.core_checks()
        acceptance.browser()
        acceptance.revoke_checks()
        acceptance.status = 'passed'
        print(f'PASS all checks; artifacts: {acceptance.output}', flush=True)
    except Exception as error:
        acceptance.error = acceptance.redact(str(error))
        raise
    finally:
        acceptance.cleanup()


if __name__ == '__main__':
    main()
