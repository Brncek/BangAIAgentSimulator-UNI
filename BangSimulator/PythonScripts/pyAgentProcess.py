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

    type = request[0]

    if type == 0:
        pass
    elif type == 1:
        pass
    else:
        pass

    #TODO implement the logic of the agent here, for now we just return the same text in uppercase
    
    #response = [
    #    True,
    #    text.upper()
    #]

    #socket.send(msgpack.packb(response))