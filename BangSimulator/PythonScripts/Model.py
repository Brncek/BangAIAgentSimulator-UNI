import enum

class AgentAction: 
    def __init__(self, actionType : int, target : int):
        self.ActionType = actionType
        self.Target = target

class GameInfo: 
    #TODO finish this class
    pass

class PlayerRole(enum):
    Sheriff = 0
    Deputy = 1
    Outlaw = 2
    Renegade = 3

class CardType(enum):
    Barrel = 0
    scope = 1
    Mustang = 2
    Dinamite = 3
    Jail = 4
    
    #guns
    Remington = 5, 
    Carabine = 6
    Schofield = 7
    Vulcanic = 8
    Winchester = 9
    
    Bang = 10 
    Beer = 11
    CatBalou = 12 #take someone's card and put it in the discard pile
    Duel = 13 #challenge someone to a duel, they have to play a bang card or lose a life point, then you have to do the same, until one of you can't play a bang card
    Gatling = 14 #play a bang card for each player, all players have to play a miss card or lose a life point
    GeneralStore = 15 #all players draw a card, starting with the player who played the card
    Indians = 16 #all players have to play a bang card or lose a life point, starting with the player who played the card
    Missed = 17 #play this card to avoid losing a life point when someone plays a bang card against you
    Panic = 18 #play this card to take a card from another player, but you can only take a card that is adjacent to you (to the left or right)
    Salon = 19 #everybody gets +1 life if possible
    Stagecoach = 20 #+ 2 cards
    WellsFargo = 21 #+ 3 cards    