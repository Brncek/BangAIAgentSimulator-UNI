import random

class pyAgent:
    def __init__(self):
        pass
    def GameOver(self, winningRole : int):
        # implement this method
        pass


    def Step(self, gameInfo) -> list:
        #gameInfo["embededState"] , gameInfo["embededCardMask"] 

        random_action = random.choice(gameInfo["avanableActions"])
        target = random.choice(random_action[1])
        resoult = [random_action[0], target, -1 , [0.0]] #if anything else than -1 the maskedActions Decoder will be used 

        return resoult


    def Reset(self):
        # implement this method
        pass

    def Rewards(self) -> list:
        # implement this method
        return [0.0]

    def SetEval(isEval : bool):
        pass

    def Save(self, path):
        # implement this method
        pass

    def Load(self, path):
        # implement this method
        pass
