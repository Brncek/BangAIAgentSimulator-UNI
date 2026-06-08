import random

class pyAgent:
    def __init__(self):
        pass
    def GameOver(self, winningRole : int):
        # mplement this method
        pass


    def Step(self, gameInfo) -> list:

        random_action = random.choice(gameInfo["avanableActions"])
        target = random.choice(random_action[1])
        action = [random_action[0], target, -1] #if anything else than -1 the maskedActions Decoder will be used 

        return action


    def Reset(self):
        #implement this method
        pass