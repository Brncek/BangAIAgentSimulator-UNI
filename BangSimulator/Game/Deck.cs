using System;
using System.Collections.Generic;
using System.Text;

namespace BangSimulator.Game
{
    public class Deck
    {
        private Stack<Card> deck;
        private List<Card> discardPile;

        public LinkedList<(Card plaied, int pId)> DeckMemory { get; set; } = [];

        public Deck()
        {
            deck = new Stack<Card>();
            discardPile = [];

            Reset();
        }

        public void Reset()
        {
            discardPile.Clear();
            deck.Clear();
            DeckMemory.Clear();

            discardPile = getDeck();
            ShuffleFromDiscard();
        }

        public Card DrawCard()
        {
            if (deck.Count == 0)
                ShuffleFromDiscard();
            return deck.Pop();
        }

        public void DiscardCard(Card card,  int playerIndex)
        {
            discardPile.Add(card);
        
            if (playerIndex != -1)
            {
                DeckMemory.AddFirst((card, playerIndex));
                if (DeckMemory.Count > 10) //TODO: configurable memory size
                    DeckMemory.RemoveLast();
            }
        }

        private void ShuffleFromDiscard()
        {
            var rnd = new Random(); //TODO: fixed seed option
            
            for (int i = discardPile.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(0, i + 1);
                var temp = discardPile[i];
                discardPile[i] = discardPile[j];
                discardPile[j] = temp;
            }

            foreach (var card in discardPile)
            {
                deck.Push(card);
            }

            discardPile.Clear();
        }

        private List<Card> getDeck()
        {
            List<Card> cards = new List<Card>();

            cards.Add(new Card(CardBangType.Barrel, CardValue.Queen, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Barrel, CardValue.King, CardSuit.Spades));

            cards.Add(new Card(CardBangType.Dinamite, CardValue.Two, CardSuit.Hearts));

            cards.Add(new Card(CardBangType.Jail, CardValue.Jack, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Jail, CardValue.Four, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Jail, CardValue.Ten, CardSuit.Spades));

            cards.Add(new Card(CardBangType.Mustang, CardValue.Eight, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Mustang, CardValue.Nine, CardSuit.Hearts));

            cards.Add(new Card(CardBangType.Remington, CardValue.King, CardSuit.Clubs));
            
            cards.Add(new Card(CardBangType.Carabine, CardValue.Ace, CardSuit.Clubs));

            cards.Add(new Card(CardBangType.Schofield, CardValue.Jack, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Schofield, CardValue.Queen, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Schofield, CardValue.King, CardSuit.Spades));

            cards.Add(new Card(CardBangType.scope, CardValue.Ace, CardSuit.Spades));

            cards.Add(new Card(CardBangType.Vulcanic, CardValue.Ten, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Vulcanic, CardValue.Ten, CardSuit.Clubs));

            cards.Add(new Card(CardBangType.Winchester, CardValue.Eight, CardSuit.Spades));

            cards.Add(new Card(CardBangType.Bang, CardValue.Ace, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Bang, CardValue.Two, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Three, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Four, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Five, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Six, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Seven, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Eight, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Nine, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Ten, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Jack, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Queen, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.King, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Ace, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Bang, CardValue.Two, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Three, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Four, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Five, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Six, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Seven, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Eight, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Nine, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Ten, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Jack, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Queen, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.King, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Ace, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Bang, CardValue.Queen, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Bang, CardValue.King, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Bang, CardValue.Ace, CardSuit.Hearts));

            cards.Add(new Card(CardBangType.Beer, CardValue.Six, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Beer, CardValue.Seven, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Beer, CardValue.Eight, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Beer, CardValue.Nine, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Beer, CardValue.Ten, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Beer, CardValue.Jack, CardSuit.Hearts));

            cards.Add(new Card(CardBangType.CatBalou, CardValue.King, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.CatBalou, CardValue.Nine, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.CatBalou, CardValue.Ten, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.CatBalou, CardValue.Jack, CardSuit.Diamonds));

            cards.Add(new Card(CardBangType.Duel, CardValue.Queen, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Duel, CardValue.Jack, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Duel, CardValue.Eight, CardSuit.Clubs));

            cards.Add(new Card(CardBangType.Gatling, CardValue.Ten, CardSuit.Hearts));

            cards.Add(new Card(CardBangType.GeneralStore, CardValue.Nine, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.GeneralStore, CardValue.Queen, CardSuit.Spades));

            cards.Add(new Card(CardBangType.Indians, CardValue.King, CardSuit.Diamonds));
            cards.Add(new Card(CardBangType.Indians, CardValue.Ace, CardSuit.Diamonds));

            cards.Add(new Card(CardBangType.Missed, CardValue.Ten, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Missed, CardValue.Jack, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Missed, CardValue.Queen, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Missed, CardValue.King, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Missed, CardValue.Ace, CardSuit.Clubs));
            cards.Add(new Card(CardBangType.Missed, CardValue.Two, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Missed, CardValue.Three, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Missed, CardValue.Four, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Missed, CardValue.Five, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Missed, CardValue.Six, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Missed, CardValue.Seven, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Missed, CardValue.Eight, CardSuit.Spades));

            cards.Add(new Card(CardBangType.Panic, CardValue.Jack, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Panic, CardValue.Queen, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Panic, CardValue.Ace, CardSuit.Hearts));
            cards.Add(new Card(CardBangType.Panic, CardValue.Eight, CardSuit.Diamonds));

            cards.Add(new Card(CardBangType.Salon, CardValue.Five, CardSuit.Hearts));

            cards.Add(new Card(CardBangType.Stagecoach, CardValue.Nine, CardSuit.Spades));
            cards.Add(new Card(CardBangType.Stagecoach, CardValue.Nine, CardSuit.Spades));

            cards.Add(new Card(CardBangType.WellsFargo, CardValue.Three, CardSuit.Hearts));

            return cards;
        }
    }
}
