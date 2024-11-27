using System.Buffers;

namespace CS120.Utils.Buffer;

public class ChunkedSequence<T>
{
    private ReadOnlyChunk<T>? _first;
    private ReadOnlyChunk<T>? _current;

    private bool _changed = false;
    private ReadOnlySequence<T>? _sequence;

    public ChunkedSequence()
    {
        _first = _current = null;
        _sequence = null;
        _changed = false;
    }

    public ChunkedSequence(ReadOnlySequence<T> sequence) : this()
    {
        Append(sequence);
    }

    public void Append(ReadOnlySequence<T> sequence)
    {
        var pos = sequence.Start;
        while (sequence.TryGet(ref pos, out ReadOnlyMemory<T> mem, true))
            Append(mem);
    }

    public void Append(ReadOnlyMemory<T> memory)
    {
        if (_current == null)
            _first = _current = new ReadOnlyChunk<T>(memory);
        else
            _current = _current.Append(memory);

        _changed = true;
    }

    internal ReadOnlySequence<T> GetSequence()
    {
        if (_changed && _first is not null && _current is not null)
            _sequence = new ReadOnlySequence<T>(_first, 0, _current, _current.Memory.Length);
        else _sequence ??= new ReadOnlySequence<T>();

        return _sequence.Value;
    }

    public static implicit operator ReadOnlySequence<T>(ChunkedSequence<T> sequence)
    {
        return sequence.GetSequence();
    }

    private sealed class ReadOnlyChunk<_T> : ReadOnlySequenceSegment<_T>
    {
        public ReadOnlyChunk(ReadOnlyMemory<_T> memory)
        {
            Memory = memory;
        }

        public ReadOnlyChunk<_T> Append(ReadOnlyMemory<_T> memory)
        {
            var nextChunk = new ReadOnlyChunk<_T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = nextChunk;
            return nextChunk;
        }
    }
}