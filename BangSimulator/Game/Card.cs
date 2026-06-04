using System;
using System.Collections.Generic;
using System.Text;

namespace BangSimulator.Game
{
    public class Card
    {
        public CardBangType Type {  get; init; }
        public CardValue Value { get; init; }
        public CardSuit Suit { get; init; }
        public CardBangColor Color => (int)Type >= (int)CardBangType.Bang ? CardBangColor.Brown : CardBangColor.Blue;


        public Card(CardBangType type, CardValue value, CardSuit suit)
        {
            Type = type;
            Value = value;
            Suit = suit;
        }

        public bool IsGun()
        {
            return Type >= CardBangType.Remington && Type <= CardBangType.Winchester;
        }

        public override string ToString()
        {
            return $"{Type} of {Suit} ({Value})";
        }
    }

    public enum CardBangType
    {
        //Blue

        //Perks
        Barrel,  
        scope,
        Mustang,

        // BadBlues
        Dinamite,
        Jail,
        //

        // Guns
        Remington, 
        Carabine, 
        Schofield,
        Vulcanic,
        Winchester,
        //

        // Brown
        Bang, // BANG!! 
        Beer, // +1 Life  (SIMULATING ENGINE AUTOMATICLAI USES BEER WHEN THE TURN STARTS)

        CatBalou, // take someone's card and put it in the discard pile
        Duel, // challenge someone to a duel, they have to play a bang card or lose a life point, then you have to do the same, until one of you can't play a bang card
        Gatling, // play a bang card for each player, all players have to play a miss card or lose a life point
        GeneralStore, // all players draw a card, starting with the player who played the card
        Indians, // all players have to play a bang card or lose a life point, starting with the player who played the card
        Missed, // play this card to avoid losing a life point when someone plays a bang card against you
        Panic, // play this card to take a card from another player, but you can only take a card that is adjacent to you (to the left or right)
        Salon, // everybody gets +1 lifr if posible
        Stagecoach, // + 2 cards
        WellsFargo // + 3 cards

    }

    public enum CardBangColor
    {
        Blue, Brown
    }

    public enum CardValue
    {
        Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace
    }

    public enum CardSuit
    {
        Hearts, Diamonds, Clubs, Spades
    }


}
