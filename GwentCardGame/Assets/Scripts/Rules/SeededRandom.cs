using System;

namespace Gwent.Rules
{
    // A battle's random source: the same seed gives the same numbers on every platform and runtime (xoshiro128**, by
    // Blackman and Vigna), so a battle replays from its seed and its moves. A System.Random for the rules that take one.
    public sealed class SeededRandom : Random
    {
        private uint s0, s1, s2, s3;

        public SeededRandom(int seed)
        {
            // The state from the seed through SplitMix32 (never all zero)
            uint x = unchecked((uint)seed);
            s0 = Mix(ref x);
            s1 = Mix(ref x);
            s2 = Mix(ref x);
            s3 = Mix(ref x);
            if ((s0 | s1 | s2 | s3) == 0)
                s0 = 1;
        }

        private static uint Mix(ref uint x)
        {
            unchecked
            {
                uint z = x += 0x9E3779B9;
                z = (z ^ (z >> 16)) * 0x85EBCA6B;
                z = (z ^ (z >> 13)) * 0xC2B2AE35;
                return z ^ (z >> 16);
            }
        }

        private uint NextUInt()
        {
            unchecked
            {
                uint result = Rotl(s1 * 5, 7) * 9;
                uint t = s1 << 9;
                s2 ^= s0;
                s3 ^= s1;
                s1 ^= s2;
                s0 ^= s3;
                s2 ^= t;
                s3 = Rotl(s3, 11);
                return result;
            }
        }

        private static uint Rotl(uint x, int k)
        {
            return (x << k) | (x >> (32 - k));
        }

        // From 0 to max - 1, unbiased (Lemire's method)
        private int Below(uint max)
        {
            if (max <= 1)
                return 0;
            unchecked
            {
                ulong m = (ulong)NextUInt() * max;
                uint low = (uint)m;
                if (low < max)
                {
                    uint threshold = (0u - max) % max;
                    while (low < threshold)
                    {
                        m = (ulong)NextUInt() * max;
                        low = (uint)m;
                    }
                }
                return (int)(m >> 32);
            }
        }

        public override int Next()
        {
            return Below(int.MaxValue);
        }

        public override int Next(int maxValue)
        {
            if (maxValue < 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            return Below((uint)maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(minValue));
            return minValue + Below((uint)((long)maxValue - minValue));
        }

        public override double NextDouble()
        {
            return Sample();
        }

        protected override double Sample()
        {
            return (NextUInt() >> 8) * (1.0 / (1u << 24));
        }

        public override void NextBytes(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)NextUInt();
        }
    }
}
