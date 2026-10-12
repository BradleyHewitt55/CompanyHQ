"""Disposable end-to-end acceptance and repeatable warm latency measurements (stdlib only)."""
from http.client import HTTPConnection
import importlib.util
import json
import os
import pathlib
import platform
import socket
import statistics
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("sdk", ROOT / "python/service.py")
sdk = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = sdk
spec.loader.exec_module(sdk)


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def wait(predicate):
    until = time.monotonic() + 45
    while time.monotonic() < until:
        try:
            if predicate():
                return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(.1)
    raise TimeoutError("process did not become ready")


def http(port, path, body=None):
    request = urllib.request.Request(f"http://127.0.0.1:{port}{path}",
                                    data=json.dumps(body).encode() if body is not None else None,
                                    headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.load(error)


def sample(function, count=200):
    for _ in range(20):
        function()
    values = []
    for _ in range(count):
        start = time.perf_counter_ns()
        function()
        values.append((time.perf_counter_ns() - start) / 1_000_000)
    return dict(samples=count, median_ms=round(statistics.median(values), 3),
                p95_ms=round(sorted(values)[int(.95 * (len(values) - 1))], 3),
                mean_ms=round(statistics.mean(values), 3))


def sample_http(port, path):
    # Warm one persistent HTTP channel to measure runtime cost, not repeated TCP setup/close.
    connection = HTTPConnection("127.0.0.1", port, timeout=10)
    try:
        def get():
            connection.request("GET", path)
            response = connection.getresponse()
            assert response.status == 200
            return json.loads(response.read())
        return sample(get)
    finally:
        connection.close()


def main():
    container = "companyhq-ual-test-" + uuid.uuid4().hex[:10]
    processes = []
    logs = []
    password = uuid.uuid4().hex  # Disposable infrastructure only, never a deployment credential.
    db_port, worker_port, api_port = free_port(), free_port(), free_port()
    try:
        subprocess.run(["dotnet", "build", str(ROOT / "UniversalApi.csproj"), "-c", "Release", "-v", "quiet"], check=True)
        subprocess.run(["docker", "run", "-d", "--rm", "--name", container, "-e", "POSTGRES_PASSWORD=" + password,
                        "-e", "POSTGRES_DB=ual", "-p", "127.0.0.1::5432", "postgres:17-alpine"], check=True, capture_output=True)
        wait(lambda: subprocess.run(["docker", "exec", container, "pg_isready", "-h", "127.0.0.1", "-U", "postgres"], capture_output=True).returncode == 0)
        mapped = subprocess.check_output(["docker", "port", container, "5432/tcp"], text=True).strip().split(":")[-1]
        env = dict(os.environ, UAL_POSTGRES=f"Host=127.0.0.1;Port={mapped};Database=ual;Username=postgres;Password={password}",
                   UAL_DB_PORT=str(db_port), UAL_SERVICE_PORT=str(worker_port), ASPNETCORE_URLS=f"http://127.0.0.1:{api_port}")
        binary = ROOT / "bin/Release/net10.0/UniversalApi.dll"

        def launch(*args):
            log = tempfile.TemporaryFile(mode="w+b")
            logs.append(log)
            process = subprocess.Popen(["dotnet", str(binary), *args], env=env, stdout=log, stderr=log)
            processes.append(process)
            return process

        launch("db")
        # Probe connection, not model metadata, then establish the reusable DB SDK connection.
        def db_ready():
            with socket.create_connection(("127.0.0.1", db_port), timeout=.1):
                return True
        wait(db_ready)
        db = sdk.DbClient(db_port)
        assert db.register(sdk.User) == {"result": True}
        assert db.call("MODEL_REGISTER", "User", fields=dict(sdk.fields(sdk.User), email="bool"))["status"] == 409
        measurements = {}
        contracts = []
        saved = None
        for language in ["csharp", "python"]:
            host = launch("api", language)
            wait(lambda: http(api_port, "/schema")[0] == 200)
            contract = http(api_port, "/schema")[1]
            contracts.append(contract)
            # This client validates only the exported schema, independent of implementation language.
            assert "models" not in contract, "private model schemas must not be exported to clients"
            assert contract["dtos"]["UserDto"] == sdk.fields(sdk.UserDto)
            if saved is not None:
                assert http(api_port, "/users/" + saved["id"]) == (200, saved)
            status, saved = http(api_port, "/users", dict(name="Native " + language, email="native@example.test"))
            assert status == 200 and set(saved) == set(contract["dtos"]["UserDto"])
            assert uuid.UUID(saved["id"]).version == 7
            assert http(api_port, "/users/" + saved["id"]) == (200, saved)
            assert http(api_port, "/users", dict(name="", email="bad"))[0] == 400
            assert http(api_port, "/users/" + str(uuid.uuid4()))[0] == 404
            assert http(api_port, "/users/not-a-uuid")[0] == 400
            assert http(api_port, "/users", [1, 2, 3])[0] == 400
            measurements[language + "_public_get"] = sample_http(api_port, "/users/" + saved["id"])
            measurements[language + "_cached_schema"] = sample_http(api_port, "/schema")
            host.terminate()
            host.wait(timeout=15)
        assert contracts[0] == contracts[1], "native metadata/DTOs changed when language switched"
        # Switch back: C# must read Python-created data through the same still-running DB service.
        host = launch("api", "csharp")
        wait(lambda: http(api_port, "/schema")[0] == 200)
        assert http(api_port, "/users/" + saved["id"]) == (200, saved)
        host.terminate()
        host.wait(timeout=15)
        native_sdk = subprocess.run(["dotnet", str(binary), "verify-db"], env=env, check=True, capture_output=True, text=True)
        print(native_sdk.stdout.strip())
        measurements["direct_db_get"] = sample(lambda: db.get(sdk.User, saved["id"]))
        # Generic model API is identical in each language; DB controls timestamps and identity.
        before = saved["createdAt"]
        updated = db.update(sdk.User, dict(saved, name="Changed"))["result"]
        assert updated["createdAt"] == before and updated["updatedAt"] >= saved["updatedAt"]
        query = dict(where=dict(op="and", args=[dict(op="contains", field="name", value="hang"),
                                                dict(op="in", field="email", value=["native@example.test"])]),
                     order="createdAt", limit=10, offset=0)
        assert updated in db.find(sdk.User, query)["result"]
        assert db.exists(sdk.User, query)["result"] is True and db.count(sdk.User, query)["result"] >= 1
        assert db.delete(sdk.User, saved["id"])["result"] is True
        assert db.get(sdk.User, saved["id"])["status"] == 404
        assert db.call("FIND", "User", query=dict(where=dict(op="eq", field="passwordHash", value="x")))["status"] == 400
        db.stream.close()
        db.socket.close()
        report = dict(platform=platform.platform(), transport="persistent bounded JSON/TCP; public HTTP loopback",
                      warmed=20, measurements=measurements)
        (ROOT / "measurements.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf8")
        (ROOT / "contract.json").write_text(json.dumps(contracts[0], indent=2) + "\n", encoding="utf8")
        types = {"string": "string", "uuid": "string", "datetime": "string"}
        declarations = []
        for name, fields in contracts[0]["dtos"].items():
            declarations.append("export interface " + name + " {\n" + "\n".join(f"  {field}: {types[kind]};" for field, kind in fields.items()) + "\n}")
        (ROOT / "client.d.ts").write_text("// Generated from language-neutral exported DTOs only.\n" + "\n\n".join(declarations) + "\n", encoding="utf8")
        print(json.dumps(report, indent=2))
        print("PASS: two native controllers, identical routes/schema, same live DB service, CRUD/query, errors, client DTOs")
    except Exception:
        for log in logs:
            log.seek(0)
            print(log.read().decode(errors="replace"))
        raise
    finally:
        for process in reversed(processes):
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=15)
        for log in logs:
            log.close()
        subprocess.run(["docker", "rm", "-f", container], capture_output=True)


if __name__ == "__main__":
    main()
