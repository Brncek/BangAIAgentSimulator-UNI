from urllib import response
import zmq
import msgpack
import sys

from pyAgent import pyAgent


agent = pyAgent()

port = sys.argv[1]

adress = "tcp://127.0.0.1:" + port  

context = zmq.Context()

socket = context.socket(zmq.PAIR)
socket.bind(adress)

while True:
   
    data = socket.recv()

    request = msgpack.unpackb(data, raw=False)

    type = request["requestType"]

    if type == 0:

        reaction = agent.Step(request)

        socket.send(msgpack.packb(reaction))

    elif type == 1:
        agent.Reset()
    else:
        agent.GameOver(request["playerRole"])

