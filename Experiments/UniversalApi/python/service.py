"""Native Python SDK + example. No HTTP server, SQL driver or roof-owned User DTO."""
import dataclasses
import datetime
import decimal
import json
import socket
import sys
import types
import typing
import uuid

MAX_BYTES = 65536


def send(stream, value):
    frame = json.dumps(value, separators=(",", ":")).encode() + b"\n"
    if len(frame) > MAX_BYTES:
        raise ValueError("frame too large")
    stream.write(frame)
    stream.flush()


def receive(stream):
    line = stream.readline(MAX_BYTES + 1)
    if not line or len(line) > MAX_BYTES or not line.endswith(b"\n"):
        raise EOFError("disconnected or oversized frame")
    return json.loads(line)


class DbClient:
    def __init__(self, port):
        self.socket = socket.create_connection(("127.0.0.1", port), timeout=10)
        self.socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.stream = self.socket.makefile("rwb")

    def call(self, operation, model, **payload):
        send(self.stream, dict(op=operation, model=model, **payload))
        return receive(self.stream)

    def register(self, model):
        return self.call("MODEL_REGISTER", model.__name__, fields=fields(model))

    def get(self, model, key):
        return self.call("GET", model.__name__, id=key)

    def save(self, model, data):
        return self.call("SAVE", model.__name__, data=data)

    def update(self, model, data):
        return self.call("UPDATE", model.__name__, data=data)

    def delete(self, model, key):
        return self.call("DELETE", model.__name__, id=key)

    def find(self, model, query):
        return self.call("FIND", model.__name__, query=query)

    def exists(self, model, query):
        return self.call("EXISTS", model.__name__, query=query)

    def count(self, model, query):
        return self.call("COUNT", model.__name__, query=query)


def api_service(name, prefix, requires):
    def mark(controller):
        controller.registration = dict(version=1, service=name, prefix=prefix, requires=requires)
        return controller
    return mark


def operation(method, route, input_type, output_type):
    def mark(function):
        function.registration = dict(name=function.__name__, method=method, route=route,
                                     input=input_type, output=output_type,
                                     errors=[400, 404, 409, 503], policy="anonymous-demo")
        return function
    return mark


def fields(shape):
    primitives = {str: "string", bool: "bool", int: "int64", decimal.Decimal: "decimal",
                  uuid.UUID: "uuid", datetime.date: "date", datetime.datetime: "datetime",
                  bytes: "bytes", dict: "json"}
    def primitive(kind):
        origin, arguments = typing.get_origin(kind), typing.get_args(kind)
        if origin is typing.Annotated and arguments == (int, "int32"):
            return "int32"
        if origin is list:
            return "list<" + primitive(arguments[0]) + ">"
        if origin in (typing.Union, types.UnionType) and type(None) in arguments:
            required = [item for item in arguments if item is not type(None)]
            if len(required) == 1:
                return "optional<" + primitive(required[0]) + ">"
        return primitives[kind]
    return {field.name: primitive(field.type) for field in dataclasses.fields(shape)}


@dataclasses.dataclass
class BaseModel:
    id: uuid.UUID
    createdAt: datetime.datetime
    updatedAt: datetime.datetime


@dataclasses.dataclass
class User(BaseModel):
    name: str
    email: str


@dataclasses.dataclass
class UserDto:
    id: uuid.UUID
    name: str
    email: str
    createdAt: datetime.datetime
    updatedAt: datetime.datetime


@dataclasses.dataclass
class CreateUserDto:
    name: str
    email: str


@api_service("users", "/users", "db:primary")
class UserController:
    def __init__(self, db):
        self.db = db

    @operation("POST", "", "CreateUserDto", "UserDto")
    def Create(self, args):
        body = args.get("body", {})
        if not isinstance(body, dict) or not isinstance(body.get("name"), str) or not body["name"].strip() or not isinstance(body.get("email"), str) or "@" not in body["email"]:
            return dict(status=400, error="invalid_user")
        return self.to_dto(self.db.save(User, dict(name=body["name"], email=body["email"])))

    @operation("GET", "/{id}", "uuid", "UserDto")
    def Get(self, args):
        try:
            uuid.UUID(args["id"])
        except (ValueError, KeyError):
            return dict(status=400, error="invalid_id")
        return self.to_dto(self.db.get(User, args["id"]))

    @staticmethod
    def to_dto(response):
        if "error" in response:
            return response
        return dict(result={name: response["result"][name] for name in ("id", "name", "email", "createdAt", "updatedAt")})


def run(port, db_port):
    db = DbClient(db_port)
    assert db.register(User).get("result") is True
    controller = UserController(db)
    # Discover once, retain bound native operations; no reflection/schema in CALL.
    calls = {name: getattr(controller, name) for name, value in vars(UserController).items()
             if hasattr(value, "registration")}
    contract = dict(UserController.registration,
                    operations=[getattr(UserController, name).registration for name in calls],
                    dtos={shape.__name__: fields(shape) for shape in [UserDto, CreateUserDto]},
                    models={"User": fields(User)})
    with socket.socket() as server:
        server.bind(("127.0.0.1", port))
        server.listen(1)
        print("ready", flush=True)
        connection, _ = server.accept()
        with connection, connection.makefile("rwb") as stream:
            while True:
                try:
                    request = receive(stream)
                    response = dict(result=contract) if request["op"] == "SERVICE_REGISTER" else calls[request["operation"]](request["args"])
                    send(stream, response)
                except EOFError:
                    break


if __name__ == "__main__":
    run(int(sys.argv[1]), int(sys.argv[2]))
