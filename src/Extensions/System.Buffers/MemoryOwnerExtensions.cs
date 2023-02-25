namespace System.Buffers;

public static class MemoryOwnerExtensions
{
    public static IMemoryOwner<T> Slice<T>(this IMemoryOwner<T> owner, int start, int count) => new MemoryOwnerSlice<T>(owner, start, count);

    private sealed class MemoryOwnerSlice<T> : IMemoryOwner<T>
    {
        private readonly IMemoryOwner<T> _owner;
        private readonly int _start;
        private readonly int _count;

        internal MemoryOwnerSlice(IMemoryOwner<T> owner, int start, int count)
        {
            _owner = owner;
            _start = start;
            _count = count;
        }

        public Memory<T> Memory => _owner.Memory.Slice(_start, _count);

        public void Dispose() => _owner.Dispose();
    }
}
