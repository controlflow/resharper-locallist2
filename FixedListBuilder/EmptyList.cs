using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace JetBrains.Util
{
  /// <summary>
  /// Reuses the single instance of an empty list (one per type). This instance is read-only and reuses singleton enumerator.
  /// </summary>
  [Serializable, DebuggerDisplay("Count = 0")]
  public class EmptyList<T> : IList<T>, IReadOnlyList<T>
  {
    // nominal type: what you want most of the time, compatible both with IList and IReadOnlyList
    [NotNull] public static readonly EmptyList<T> Instance = new EmptyList<T>();

    // IList type: when you need for IList type being inferred (may be useful in ?: or ?? operands)
    [NotNull] public static readonly IList<T> InstanceList = Instance;
    [NotNull] public static readonly IReadOnlyList<T> ReadOnly = Instance;
    [NotNull] public static readonly IReadOnlyCollection<T> Collection = Instance;
    [NotNull] public static readonly IEnumerable<T> Enumerable = Instance;

    protected EmptyList() { }

    public IEnumerator<T> GetEnumerator() => EmptyEnumerator<T>.Instance;
    IEnumerator IEnumerable.GetEnumerator() => EmptyEnumerator<T>.Instance;

    public void Add(T item) => throw new CollectionReadOnlyException();
    public bool Remove(T item) => throw new CollectionReadOnlyException();
    public void Clear() => throw new CollectionReadOnlyException();
    public void Insert(int index, T item) => throw new CollectionReadOnlyException();
    public void RemoveAt(int index) => throw new CollectionReadOnlyException();

    public bool Contains(T item) => false;
    public int IndexOf(T item) => -1;

    public int Count => 0;
    public bool IsReadOnly => true;

    public void CopyTo(T[] array, int arrayIndex)
    {
      if (array == null)
        throw new ArgumentNullException(nameof(array));

      if (arrayIndex < 0 || arrayIndex > array.Length)
        throw new ArgumentOutOfRangeException(nameof(arrayIndex));
    }

    public T this[int index]
    {
      get => throw new ArgumentOutOfRangeException();
      set => throw new CollectionReadOnlyException();
    }
  }

  /// <summary>Enumerator for empty collection.</summary>
  /// <typeparam name="T"></typeparam>
  public sealed class EmptyEnumerator<T> : IEnumerator<T>
  {
    [NotNull]
    public static readonly EmptyEnumerator<T> Instance = new EmptyEnumerator<T>();

    public T Current => throw new InvalidOperationException("EmptyEnumerator.Current is undefined");

    object IEnumerator.Current => Current;

    public bool MoveNext() => false;

    public void Reset()
    {
    }

    public void Dispose()
    {
    }
  }


  [Serializable]
  public class CollectionReadOnlyException : NotSupportedException
  {
    public CollectionReadOnlyException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public CollectionReadOnlyException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public CollectionReadOnlyException(string message) : base(message)
    {
    }

    public CollectionReadOnlyException() : base("Collection is read-only")
    {
    }
  }
}