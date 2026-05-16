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

        response_bytes = msgpack.packb(reaction)

        pipe.write(
            struct.pack(
                "i",
                len(response_bytes)
                )
        )

        pipe.write(response_bytes)
        pipe.flush()

    elif type == 1:
        agent.Reset()
    else:
        agent.GameOver(request["playerRole"])

