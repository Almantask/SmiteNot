using System;
using System.Buffers.Binary;

namespace GodGame.Sim.Math
{
    /// <summary>
    /// Streaming xxHash64 (XXH64). Used for state hashes, keyframe hashes and deriving random streams.
    /// Values are fed little-endian, so the hash is identical on every platform and runtime.
    /// </summary>
    public sealed class SimHash
    {
        private const ulong Prime1 = 0x9E3779B185EBCA87UL;
        private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
        private const ulong Prime3 = 0x165667B19E3779F9UL;
        private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;
        private const ulong Prime5 = 0x27D4EB2F165667C5UL;
        private const int StripeLength = 32;

        private readonly ulong _seed;
        private readonly byte[] _buffer = new byte[StripeLength];
        private readonly byte[] _scratch = new byte[8];
        private int _buffered;
        private ulong _length;
        private ulong _v1;
        private ulong _v2;
        private ulong _v3;
        private ulong _v4;

        public SimHash(ulong seed = 0)
        {
            _seed = seed;
            Reset();
        }

        public void Reset()
        {
            unchecked
            {
                _v1 = _seed + Prime1 + Prime2;
                _v2 = _seed + Prime2;
                _v3 = _seed;
                _v4 = _seed - Prime1;
            }
            _buffered = 0;
            _length = 0;
        }

        /// <summary>One-shot hash of a byte span.</summary>
        public static ulong Hash64(ReadOnlySpan<byte> data, ulong seed = 0)
        {
            var hash = new SimHash(seed);
            hash.Append(data);
            return hash.Digest();
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            _length += (ulong)data.Length;

            if (_buffered > 0)
            {
                int take = System.Math.Min(StripeLength - _buffered, data.Length);
                data.Slice(0, take).CopyTo(_buffer.AsSpan(_buffered));
                _buffered += take;
                data = data.Slice(take);
                if (_buffered < StripeLength)
                {
                    return;
                }
                ConsumeStripe(_buffer);
                _buffered = 0;
            }

            while (data.Length >= StripeLength)
            {
                ConsumeStripe(data.Slice(0, StripeLength));
                data = data.Slice(StripeLength);
            }

            data.CopyTo(_buffer);
            _buffered = data.Length;
        }

        public void AppendInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_scratch, value);
            Append(_scratch.AsSpan(0, 4));
        }

        public void AppendInt64(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(_scratch, value);
            Append(_scratch.AsSpan(0, 8));
        }

        public void AppendUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(_scratch, value);
            Append(_scratch.AsSpan(0, 8));
        }

        public void AppendInt32s(ReadOnlySpan<int> values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                AppendInt32(values[i]);
            }
        }

        /// <summary>The hash of everything appended so far. Does not change the state, so appending can continue.</summary>
        public ulong Digest()
        {
            unchecked
            {
                ulong h;
                if (_length >= StripeLength)
                {
                    h =
                        RotateLeft(_v1, 1)
                        + RotateLeft(_v2, 7)
                        + RotateLeft(_v3, 12)
                        + RotateLeft(_v4, 18);
                    h = MergeRound(h, _v1);
                    h = MergeRound(h, _v2);
                    h = MergeRound(h, _v3);
                    h = MergeRound(h, _v4);
                }
                else
                {
                    h = _seed + Prime5;
                }

                h += _length;

                ReadOnlySpan<byte> tail = _buffer.AsSpan(0, _buffered);
                while (tail.Length >= 8)
                {
                    h ^= Round(0, BinaryPrimitives.ReadUInt64LittleEndian(tail));
                    h = RotateLeft(h, 27) * Prime1 + Prime4;
                    tail = tail.Slice(8);
                }
                if (tail.Length >= 4)
                {
                    h ^= BinaryPrimitives.ReadUInt32LittleEndian(tail) * Prime1;
                    h = RotateLeft(h, 23) * Prime2 + Prime3;
                    tail = tail.Slice(4);
                }
                for (int i = 0; i < tail.Length; i++)
                {
                    h ^= tail[i] * Prime5;
                    h = RotateLeft(h, 11) * Prime1;
                }

                h ^= h >> 33;
                h *= Prime2;
                h ^= h >> 29;
                h *= Prime3;
                h ^= h >> 32;
                return h;
            }
        }

        private void ConsumeStripe(ReadOnlySpan<byte> stripe)
        {
            _v1 = Round(_v1, BinaryPrimitives.ReadUInt64LittleEndian(stripe));
            _v2 = Round(_v2, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(8)));
            _v3 = Round(_v3, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(16)));
            _v4 = Round(_v4, BinaryPrimitives.ReadUInt64LittleEndian(stripe.Slice(24)));
        }

        private static ulong Round(ulong acc, ulong input)
        {
            unchecked
            {
                acc += input * Prime2;
                acc = RotateLeft(acc, 31);
                return acc * Prime1;
            }
        }

        private static ulong MergeRound(ulong acc, ulong value)
        {
            unchecked
            {
                acc ^= Round(0, value);
                return acc * Prime1 + Prime4;
            }
        }

        private static ulong RotateLeft(ulong value, int bits) =>
            (value << bits) | (value >> (64 - bits));
    }
}
