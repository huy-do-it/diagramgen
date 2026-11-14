"""WF-01 browser acceptance on a private real 11-container stack. No fake services or injected membership."""
import argparse
import hashlib
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


class WorkspaceAcceptance:
    def source_snapshot(self):
        files = subprocess.check_output(
            ['git', 'ls-files', '--cached', '--others', '--exclude-standard', '--', 'src', 'client'],
            cwd=self.repo, text=True).splitlines()
        return {name: hashlib.sha256((self.repo / name).read_bytes()).hexdigest()
                for name in sorted(set(files)) if (self.repo / name).is_file()}

    def browser_flow(self):
        state = {'clientUrl': self.client, 'password': self.password, 'runId': self.run_id,
                 'output': str(self.output)}
        with tempfile.NamedTemporaryFile(mode='w', suffix='.json', prefix='umlforge-wf01-',
                                         encoding='utf8', delete=False) as file:
            json.dump(state, file)
            temporary = Path(file.name)
        try:
            env = os.environ.copy()
            env['UMLFORGE_WORKSPACE_E2E_STATE'] = str(temporary)
            process = subprocess.Popen(['node', str(Path(__file__).with_name('workspace_flow.browser.mjs'))],
                                       env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                       text=True, encoding='utf8', errors='replace')
            with (self.output / 'browser.log').open('w', encoding='utf8') as log:
                for line in process.stdout:
                    safe = self.redact(line)
                    log.write(safe)
                    log.flush()
                    print(safe, end='', flush=True)
            if process.wait():
                raise AssertionError('WF-01 browser failed; see browser.log and failure screenshot')
            self.passed('WF-01 browser flow uses real UI login/create/invite/accept/notification/jobs')
        finally:
            temporary.unlink(missing_ok=True)

    def __init__(self, args):
        self.repo = Path(__file__).resolve().parents[2]
        self.source_dirty = bool(subprocess.check_output(
            ['git', 'status', '--porcelain', '--', 'src', 'client'], cwd=self.repo, text=True).strip())
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
                       '--label', f'umlforge.source.dirty={str(self.source_dirty).lower()}',
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
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    args.identity_source = args.workspace_source = args.gateway_source = str(repo)
    acceptance = WorkspaceAcceptance(args)
    snapshot = acceptance.source_snapshot()
    (acceptance.output / 'source-snapshot.json').write_text(json.dumps(snapshot, indent=2), encoding='utf8')
    try:
        acceptance.build()
        acceptance.prepare()
        acceptance.browser_flow()
        acceptance.check(snapshot == acceptance.source_snapshot(), 'application source snapshot unchanged during build/test')
        acceptance.status = 'passed'
    except Exception as error:
        acceptance.error = acceptance.redact(str(error))
        print('FAIL ' + acceptance.error, flush=True)
    finally:
        acceptance.cleanup()
        result_file = acceptance.output / 'result.json'
        result = json.loads(result_file.read_text(encoding='utf8'))
        result['workflow'] = 'WF-01'
        result['sourceDirty'] = acceptance.source_dirty
        result['sourceSnapshotFile'] = 'source-snapshot.json'
        remaining = acceptance.docker('ps', '-a', '--filter', f'name=umlforge-e2e-{acceptance.run_id}',
                                      '--format', '{{.Names}}', check=False)
        result['cleanup'] = {'remainingOwnContainers': remaining.splitlines(), 'projectVolumesDeleted': False}
        result_file.write_text(json.dumps(result, indent=2), encoding='utf8')
    print(f'{acceptance.status.upper()} WF-01; artifacts: {acceptance.output}', flush=True)
    raise SystemExit(0 if acceptance.status == 'passed' else 1)


if __name__ == '__main__':
    main()
