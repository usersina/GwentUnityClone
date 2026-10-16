using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // One shared random source (separate System.Random instances created in the same tick repeat each other)
    public static class Rng
    {
        public static Random Shared { get; private set; } = new Random();

        // Makes a run reproducible (tests, autoplay)
        public static void Seed(int seed)
        {
            Shared = new Random(seed);
        }

        // Fisher-Yates shuffle in place, with the given random source
        public static void Shuffle<T>(List<T> list, Random rnd)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        // Fisher-Yates shuffle into a new list
        public static List<T> Shuffle<T>(IEnumerable<T> items)
        {
            return Shuffled(items, Shared);
        }

        // The same with the given random source
        public static List<T> Shuffled<T>(IEnumerable<T> items, Random rnd)
        {
            List<T> list = new List<T>(items);
            Shuffle(list, rnd);
            return list;
        }
    }
}
