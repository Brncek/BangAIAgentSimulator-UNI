using BangSimulator.Game;

namespace BangSimulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Deck deck = new Deck();

            for (int i = 0; i < 15; i++)
            {
                Console.WriteLine(deck.DrawCard());
            }
        }
    }
}
