from urllib import response
import sys
import struct
import msgpack


from pyAgent import pyAgent


agent = pyAgent()

PIPE_NAME = sys.argv[1]

pipe = open(
    rf'\\.\pipe\{PIPE_NAME}',
    'r+b',
    buffering=0
)

def read_exact(pipe_handle, size):
    buffer = b''

    while len(buffer) < size:
        chunk = pipe_handle.read(
            size - len(buffer)
        )

        if not chunk:
            raise Exception("Pipe closed")

        buffer += chunk

    return buffer

def sendData(pipe_handle, data : list):
    response_bytes = msgpack.packb(data)

    pipe_handle.write(
        struct.pack(
            "i",
            len(response_bytes)
            )
    )

    pipe_handle.write(response_bytes)
    pipe_handle.flush()

print("PYAGENT STARTED");

while True:

    size_data = read_exact(pipe, 4)

    request_size = struct.unpack(
        "i",
        size_data
    )[0]

    payload = read_exact(
        pipe,
        request_size
    )

    request = msgpack.unpackb(payload, raw=False)

    type = request["requestType"]

    if type == 0:
        reaction = agent.Step(request)
        sendData(pipe, reaction)

    elif type == 1:
        agent.Reset()

    elif type == 2:
        agent.GameOver(request["playerRole"])

    elif type == 3:
        res = agent.Rewards() 
        sendData(pipe, [0,0,0, res])

    elif type == 4:
        agent.SetEval(request["eval"])

    elif type == 5:
        agent.Save(request["path"])

    elif type == 6:
        agent.Load(request["path"])

    elif type == 7:
        break;


