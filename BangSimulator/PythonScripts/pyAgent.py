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
        action = [random_action[0], target]

        return action


    def Reset(self):
        #implement this method
        pass