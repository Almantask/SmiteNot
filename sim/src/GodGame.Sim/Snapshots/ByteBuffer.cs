using System;
using System.Buffers.Binary;

namespace GodGame.Sim
{
    /// <summary>A growable little-endian byte writer for snapshots: the same bytes on every platform.</summary>
    public sealed class ByteWriter
    {
        private byte[] _buffer;
        private int _length;

        public ByteWriter(int initialCapacity = 4096)
        {
            _buffer = new byte[System.Math.Max(16, initialCapacity)];
        }

        public int Length => _length;

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(Reserve(4), value);
        }

        public void WriteInt64(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(Reserve(8), value);
        }

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(Reserve(8), value);
        }

        public void WriteByte(byte value)
        {
            Reserve(1)[0] = value;
        }

        public void WriteInt32s(ReadOnlySpan<int> values)
        {
            WriteInt32(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                WriteInt32(values[i]);
            }
        }

        public byte[] ToArray()
        {
            var result = new byte[_length];
            Array.Copy(_buffer, result, _length);
            return result;
        }

        private Span<byte> Reserve(int count)
        {
            if (_length + count > _buffer.Length)
            {
                var bigger = new byte[System.Math.Max(_buffer.Length * 2, _length + count)];
                Array.Copy(_buffer, bigger, _length);
                _buffer = bigger;
            }
            var span = new Span<byte>(_buffer, _length, count);
            _length += count;
            return span;
        }
    }

    /// <summary>Reads what <see cref="ByteWriter"/> wrote; a short or malformed buffer throws <see cref="FormatException"/>.</summary>
    public sealed class ByteReader
    {
        private readonly byte[] _buffer;
        private int _position;

        public ByteReader(byte[] buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public bool AtEnd => _position == _buffer.Length;

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

        public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

        public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

        public byte ReadByte() => Take(1)[0];

        /// <summary>Reads a length-prefixed int array into <paramref name="target"/>, which must have exactly that length.</summary>
        public void ReadInt32s(Span<int> target)
        {
            int count = ReadInt32();
            if (count != target.Length)
            {
                throw new FormatException(
                    "Snapshot array length does not match the world's capacity."
                );
            }
            for (int i = 0; i < count; i++)
            {
                target[i] = ReadInt32();
            }
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            if (_position + count > _buffer.Length)
            {
                throw new FormatException("Snapshot ends early.");
            }
            var span = new ReadOnlySpan<byte>(_buffer, _position, count);
            _position += count;
            return span;
        }
    }
}
