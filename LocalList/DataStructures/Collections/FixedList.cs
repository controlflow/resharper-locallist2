using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace JetBrains.Util.DataStructures.Collections
{
  public static class FixedList
  {
    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T item)
      => new ListOf1<T>(item);

    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T first, T second)
      => new ListOf2<T>(first, second);

    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T first, T second, T third)
      => new ListOf3<T>(first, second, third);

    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T first, T second, T third, T fourth)
      => new ListOf4<T>(first, second, third, fourth);

    [NotNull, ItemNotNull, Pure]
    public static IReadOnlyList<T> OfNotNull<T>([CanBeNull] T first)
      where T : class
    {
      return first == null ? EmptyList<T>.Instance : Of(first);
    }

    [NotNull, ItemNotNull, Pure]
    public static IReadOnlyList<T> OfNotNull<T>([CanBeNull] T first, [CanBeNull] T second)
      where T : class
    {
      if (first == null) return OfNotNull(second);
      if (second == null) return Of(first);

      return Of(first, second);
    }

    [NotNull, ItemNotNull, Pure]
    public static IReadOnlyList<T> OfNotNull<T>([CanBeNull] T first, [CanBeNull] T second, [CanBeNull] T third)
      where T : class
    {
      if (first == null) return OfNotNull(second, third);
      if (second == null) return OfNotNull(first, third);
      if (third == null) return OfNotNull(first, second);

      return Of(first, second, third);
    }

    [NotNull, ItemNotNull, Pure]
    public static IReadOnlyList<T> OfNotNull<T>(
      [CanBeNull] T first, [CanBeNull] T second, [CanBeNull] T third, [CanBeNull] T fourth)
      where T : class
    {
      if (first == null) return OfNotNull(second, third, fourth);
      if (second == null) return OfNotNull(first, third, fourth);
      if (third == null) return OfNotNull(first, second, fourth);
      if (fourth == null) return OfNotNull(first, second, third);

      return Of(first, second, third, fourth);
    }

    [NotNull, Pure]
    public static IReadOnlyList<T> FromArray<T>(T[] array, int countUsed)
    {
      switch (countUsed)
      {
        case 0: return EmptyList<T>.Instance;
        case 1: return Of(array[0]);
        case 2: return Of(array[0], array[1]);
        case 3: return Of(array[0], array[1], array[2]);
        case 4: return Of(array[0], array[1], array[2], array[3]);
        default: return new ListOfArray<T>(array, countUsed);
      }
    }

    // todo: valuetype mutable enumerator
    [DebuggerTypeProxy(typeof(BuilderDebugView<>))]
    internal abstract class Builder<T> : IReadOnlyList<T>, IEnumerator<T>, IList<T>
    {
      // ReSharper disable once InconsistentNaming
      protected int myCountAndIterationData;

      // frozen flag is stored in highest bit
      protected bool IsFrozen => myCountAndIterationData >= 0;

      // count is stored in 15 bits (16-30)
      // todo: can this count only be used for IList<T> impl? to avoid << 1 shift
      public int Count => (int) ((uint) myCountAndIterationData << 1 >> CountShift + 1);

      protected const int CountShift = 16;
      protected const int MaxCount = (int) (uint.MaxValue >> CountShift + 1);
      protected const int FrozenCount = unchecked((int) (1U << 31));
      protected const int FrozenCount0 = unchecked((int) (1U << 31));
      protected const int FrozenCount1 = unchecked((int) (1U << 31 | 1U << CountShift));
      protected const int FrozenCount2 = unchecked((int) (1U << 31 | 2U << CountShift));
      protected const int FrozenCount3 = unchecked((int) (1U << 31 | 3U << CountShift));
      protected const int FrozenCount4 = unchecked((int) (1U << 31 | 4U << CountShift));

      // iterator/version data is stored in first 16 bits
      // but we include count there as well
      protected int Version => myCountAndIterationData;

      protected const int IteratorOrVersionMask = (1 << CountShift) - 1;
      protected const int VersionAndCountIncrement = (1 << CountShift) + 1;

      protected const int ReadyForGetEnumerator = IteratorOrVersionMask;

      protected Builder()
      {
        myCountAndIterationData = int.MinValue; // not frozen, count = 0, version = 0
      }

      protected Builder(int count)
      {
        Debug.Assert(count > 0); // use 'EmptyList<T>.Instance' instead
        Debug.Assert(count <= MaxCount);

        myCountAndIterationData = count << CountShift | ReadyForGetEnumerator; // frozen, count is set
      }

      public abstract int Capacity { get; }

      [Pure]
      public abstract ref T GetItemNoRangeCheck(int index);

      public abstract void Append(in T value, ref Builder<T> self);

      [NotNull] public abstract Builder<T> Clone();

      protected abstract void CopyToImpl([NotNull] T[] array, int arrayIndex);

      internal struct BuilderEnumerator
      {
        // todo: version checking
      }

      #region Frozen enumeration

      public IEnumerator<T> GetEnumerator()
      {
        Debug.Assert(IsFrozen);

        var data = myCountAndIterationData;
        var count = data >> CountShift;
        var expected = data | ReadyForGetEnumerator;

        if (expected == Interlocked.CompareExchange(
              location1: ref myCountAndIterationData, value: data | count, comparand: expected))
        {
          return this;
        }

        return new Enumerator(this);
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      private sealed class Enumerator : IEnumerator<T>
      {
        [NotNull] private readonly Builder<T> myBuilder;
        private int myIndex;

        public Enumerator([NotNull] Builder<T> builder)
        {
          Debug.Assert(builder.IsFrozen);

          myBuilder = builder;
        }

        public bool MoveNext()
        {
          var nextIndex = myIndex + 1;
          if (nextIndex < myBuilder.Count)
          {
            myIndex = nextIndex;
            return true;
          }

          return false;
        }

        public T Current => myBuilder.GetItemNoRangeCheck(myIndex);
        object IEnumerator.Current => Current;

        public void Dispose() { }
        public void Reset() { }
      }

      bool IEnumerator.MoveNext()
      {
        Debug.Assert(IsFrozen);

        var newData = myCountAndIterationData - 1;
        if (newData < 0) return false;

        myCountAndIterationData = newData;
        return true;
      }

      void IDisposable.Dispose()
      {
        Interlocked.Exchange(
          ref myCountAndIterationData, value: myCountAndIterationData | ReadyForGetEnumerator);
      }

      void IEnumerator.Reset()
      {
        myCountAndIterationData |= ~IteratorOrVersionMask;
      }

      public abstract T Current { get; }
      object IEnumerator.Current => Current;

      #endregion
      #region Read access

      public virtual T this[int index]
      {
        get => throw new InvalidOperationException("Must be overriden");
        set => throw new CollectionReadOnlyException();
      }

      public abstract int IndexOf(T item);
      public bool Contains(T item) => IndexOf(item) >= 0;

      public void CopyTo(T[] array, int arrayIndex)
      {
        if (array == null)
          throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0)
          throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (arrayIndex + Count > array.Length)
          throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        CopyToImpl(array, arrayIndex);
      }

      #endregion
      #region Write access

      public bool IsReadOnly => true;

      public void Add(T item) => throw new CollectionReadOnlyException();
      public void Insert(int index, T item) => throw new CollectionReadOnlyException();
      public void RemoveAt(int index) => throw new CollectionReadOnlyException();
      public bool Remove(T item) => throw new CollectionReadOnlyException();
      public void Clear() => throw new CollectionReadOnlyException();

      #endregion

      protected static void ThrowOutOfRange()
      {
        // ReSharper disable once NotResolvedInText
        throw new ArgumentOutOfRangeException("index", "Index should be non-negative and less than Count");
      }

      public sealed override string ToString() => $"{nameof(FixedList)}(Count = {Count.ToString()})";
    }

    internal sealed class ListOf1<T> : Builder<T>
    {
      private T myItem0;

      public ListOf1() {  }

      private ListOf1([NotNull] ListOf1<T> other)
      {
        myCountAndIterationData = other.myCountAndIterationData;
        myItem0 = other.myItem0;
      }

      public ListOf1(T item0) : base(count: 1)
      {
        myItem0 = item0;
      }

      public override T this[int index]
      {
        get
        {
          Debug.Assert(Count == 1);

          if (index != 0) ThrowOutOfRange();

          return myItem0;
        }
      }

      public override int Capacity => 1;

      public override ref T GetItemNoRangeCheck(int index)
      {
        return ref myItem0;
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch ((myCountAndIterationData += VersionAndCountIncrement) & ~IteratorOrVersionMask)
        {
          case FrozenCount + 1:
            myItem0 = value;
            break;

          default:
            self = new ListOf4<T>(myItem0, value);
            break;
        }
      }

      public override Builder<T> Clone()
      {
        return new ListOf1<T>(this);
      }

      public override T Current => myItem0;

      public override int IndexOf(T item)
      {
        if (EqualityComparer<T>.Default.Equals(item, myItem0)) return 0;

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        array[arrayIndex] = myItem0;
      }
    }

    internal sealed class ListOf2<T> : Builder<T>
    {
      private T myItem0, myItem1;

      public ListOf2() { }

      public ListOf2([NotNull] ListOf2<T> other)
      {
        myCountAndIterationData = other.myCountAndIterationData;
        myItem0 = other.myItem0;
        myItem1 = other.myItem1;
      }

      public ListOf2(T item0, T item1) : base(count: 2)
      {
        myItem0 = item0;
        myItem1 = item1;
      }

      public override T this[int index]
      {
        get
        {
          // todo: count
          if ((uint) index > 1u) ThrowOutOfRange();

          return index == 0 ? myItem0 : myItem1;
        }
      }

      public override int Capacity => 2;

      public override ref T GetItemNoRangeCheck(int index)
      {
        if (index == 0)
          return ref myItem0;

        return ref myItem1;
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch ((myCountAndIterationData += VersionAndCountIncrement) & ~IteratorOrVersionMask)
        {
          case FrozenCount1: myItem0 = value; break;
          case FrozenCount2: myItem1 = value; break;
          default: self = new ListOf4<T>(myItem0, myItem1, value); break;
        }
      }

      public override Builder<T> Clone()
      {
        return new ListOf2<T>(this);
      }

      public override T Current
      {
        get
        {
          var index = myCountAndIterationData | IteratorOrVersionMask;
          return index == 1 ? myItem1 : myItem0;
        }
      }

      public override int IndexOf(T item)
      {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(item, myItem0)) return 0;
        if (comparer.Equals(item, myItem1)) return 1;

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        array[arrayIndex++] = myItem0;
        array[arrayIndex] = myItem1;
      }
    }

    internal sealed class ListOf3<T> : Builder<T>
    {
      private T myItem0, myItem1, myItem2;

      public ListOf3() { }

      public ListOf3(T item0, T item1, T item2) : base(count: 3)
      {
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
      }

      private ListOf3([NotNull] ListOf3<T> other)
      {
        myCountAndIterationData = other.myCountAndIterationData;
        myItem0 = other.myItem0;
        myItem1 = other.myItem1;
        myItem2 = other.myItem2;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index > 2u) ThrowOutOfRange();

          switch (index)
          {
            case 0: return myItem0;
            case 1: return myItem1;
            default: return myItem2;
          }
        }
      }

      public override int Capacity => 3;

      public override ref T GetItemNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref myItem0;
          case 1: return ref myItem1;
          default: return ref myItem2;
        }
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch ((myCountAndIterationData += VersionAndCountIncrement) & ~IteratorOrVersionMask)
        {
          case FrozenCount1: myItem0 = value; break;
          case FrozenCount2: myItem1 = value; break;
          case FrozenCount3: myItem2 = value; break;
          default: self = new ListOf8<T>(myItem0, myItem1, myItem2, value); break;
        }
      }

      public override Builder<T> Clone()
      {
        return new ListOf3<T>(this);
      }

      public override T Current
      {
        get
        {
          switch (myCountAndIterationData | IteratorOrVersionMask)
          {
            case 2: return myItem0;
            case 1: return myItem1;
            default: return myItem2;
          }
        }
      }

      public override int IndexOf(T item)
      {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(item, myItem0)) return 0;
        if (comparer.Equals(item, myItem1)) return 1;
        if (comparer.Equals(item, myItem2)) return 2;

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        array[arrayIndex++] = myItem0;
        array[arrayIndex++] = myItem1;
        array[arrayIndex] = myItem2;
      }
    }

    internal sealed class ListOf4<T> : Builder<T>
    {
      private T myItem0, myItem1, myItem2, myItem3;

      public ListOf4() { }

      public ListOf4(in T item0, in T item1)
      {
        myCountAndIterationData = int.MinValue | (2 << CountShift);
        myItem0 = item0;
        myItem1 = item1;
      }

      public ListOf4(in T item0, in T item1, in T item2)
      {
        myCountAndIterationData = int.MinValue | (3 << CountShift);
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
      }

      public ListOf4(in T item0, in T item1, in T item2, in T item3)
      {
        myCountAndIterationData = int.MinValue | (4 << CountShift);
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
        myItem3 = item3;
      }

      public ListOf4(T item0, T item1, T item2, T item3) : base(count: 4)
      {
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
        myItem3 = item3;
      }

      private ListOf4([NotNull] ListOf4<T> other)
      {
        myCountAndIterationData = other.myCountAndIterationData;
        myItem0 = other.myItem0;
        myItem1 = other.myItem1;
        myItem2 = other.myItem2;
        myItem3 = other.myItem3;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index > 3u) ThrowOutOfRange();

          switch (index)
          {
            case 0: return myItem0;
            case 1: return myItem1;
            case 2: return myItem2;
            default: return myItem3;
          }
        }
      }

      public override int Capacity => 4;

      public override ref T GetItemNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref myItem0;
          case 1: return ref myItem1;
          case 2: return ref myItem2;
          default: return ref myItem3;
        }
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch ((myCountAndIterationData += VersionAndCountIncrement) & ~IteratorOrVersionMask)
        {
          case FrozenCount1: myItem0 = value; break;
          case FrozenCount2: myItem1 = value; break;
          case FrozenCount3: myItem2 = value; break;
          case FrozenCount4: myItem3 = value; break;
          default: self = new ListOf8<T>(myItem0, myItem1, myItem2, myItem3, value); break;
        }
      }

      public override Builder<T> Clone()
      {
        return new ListOf4<T>(this);
      }

      public override T Current
      {
        get
        {
          switch (myCountAndIterationData | IteratorOrVersionMask)
          {
            case 3: return myItem0;
            case 2: return myItem1;
            case 1: return myItem2;
            default: return myItem3;
          }
        }
      }

      public override int IndexOf(T item)
      {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(item, myItem0)) return 0;
        if (comparer.Equals(item, myItem1)) return 1;
        if (comparer.Equals(item, myItem2)) return 2;
        if (comparer.Equals(item, myItem3)) return 3;

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        array[arrayIndex++] = myItem0;
        array[arrayIndex++] = myItem1;
        array[arrayIndex++] = myItem2;
        array[arrayIndex] = myItem3;
      }
    }

    internal sealed class ListOf8<T> : Builder<T>
    {
      private T myItem0, myItem1, myItem2, myItem3, myItem4, myItem5, myItem6, myItem7;

      public ListOf8() { }

      public ListOf8(in T item0, in T item1, in T item2, in T item3)
      {
        myCountAndIterationData = int.MinValue | (4 << CountShift);
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
        myItem3 = item3;
      }

      public ListOf8(in T item0, in T item1, in T item2, in T item3, in T item4)
      {
        myCountAndIterationData = int.MinValue | (5 << CountShift);
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
        myItem3 = item3;
        myItem4 = item4;
      }

      public ListOf8(T item0, T item1, T item2, T item3, T item4, T item5, T item6, T item7) : base(count: 8)
      {
        myItem0 = item0;
        myItem1 = item1;
        myItem2 = item2;
        myItem3 = item3;
        myItem4 = item4;
        myItem5 = item5;
        myItem6 = item6;
        myItem7 = item7;
      }

      private ListOf8([NotNull] ListOf8<T> other)
      {
        myCountAndIterationData = other.myCountAndIterationData;
        myItem0 = other.myItem0;
        myItem1 = other.myItem1;
        myItem2 = other.myItem2;
        myItem3 = other.myItem3;
        myItem4 = other.myItem4;
        myItem5 = other.myItem5;
        myItem6 = other.myItem6;
        myItem7 = other.myItem7;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index > 7u) ThrowOutOfRange();

          switch (index)
          {
            case 0: return myItem0;
            case 1: return myItem1;
            case 2: return myItem2;
            case 3: return myItem3;
            case 4: return myItem4;
            case 5: return myItem5;
            case 6: return myItem6;
            default: return myItem7;
          }
        }
      }

      public override int Capacity => 8;

      public override ref T GetItemNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref myItem0;
          case 1: return ref myItem1;
          case 2: return ref myItem2;
          case 3: return ref myItem3;
          case 4: return ref myItem4;
          case 5: return ref myItem5;
          case 6: return ref myItem6;
          default: return ref myItem7;
        }
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch ((myCountAndIterationData += VersionAndCountIncrement) & ~IteratorOrVersionMask)
        {
          case FrozenCount + 1: myItem0 = value; break;
          case FrozenCount + 2: myItem1 = value; break;
          case FrozenCount + 3: myItem2 = value; break;
          case FrozenCount + 4: myItem3 = value; break;
          case FrozenCount + 5: myItem4 = value; break;
          case FrozenCount + 6: myItem5 = value; break;
          case FrozenCount + 7: myItem6 = value; break;
          case FrozenCount + 8: myItem7 = value; break;
          default: self = new ListOfArray<T>(myItem0, myItem1, myItem2, myItem3, myItem4, myItem5, myItem6, myItem7, value); break;
        }
      }

      public override Builder<T> Clone()
      {
        return new ListOf8<T>(this);
      }

      public override T Current
      {
        get
        {
          switch (myCountAndIterationData | IteratorOrVersionMask)
          {
            case 7: return myItem0;
            case 6: return myItem1;
            case 5: return myItem2;
            case 4: return myItem3;
            case 3: return myItem4;
            case 2: return myItem5;
            case 1: return myItem6;
            default: return myItem7;
          }
        }
      }

      public override int IndexOf(T item)
      {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(item, myItem0)) return 0;
        if (comparer.Equals(item, myItem1)) return 1;
        if (comparer.Equals(item, myItem2)) return 2;
        if (comparer.Equals(item, myItem3)) return 3;
        if (comparer.Equals(item, myItem4)) return 4;
        if (comparer.Equals(item, myItem5)) return 5;
        if (comparer.Equals(item, myItem6)) return 6;
        if (comparer.Equals(item, myItem7)) return 7;

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        array[arrayIndex++] = myItem0;
        array[arrayIndex++] = myItem1;
        array[arrayIndex++] = myItem2;
        array[arrayIndex++] = myItem3;
        array[arrayIndex++] = myItem4;
        array[arrayIndex++] = myItem5;
        array[arrayIndex++] = myItem6;
        array[arrayIndex] = myItem7;
      }
    }

    internal sealed class ListOfArray<T> : Builder<T>
    {
      private T[] myArray;
      private int myCount; // use this instead of myCountAndVersionData

      public ListOfArray(in T item0, in T item1, in T item2, in T item3, in T item4, in T item5, in T item6, in T item7, in T item8)
      {
        var array = new T[16];
        array[0] = item0;
        array[1] = item1;
        array[2] = item2;
        array[3] = item3;
        array[4] = item4;
        array[5] = item5;
        array[6] = item6;
        array[7] = item7;
        array[8] = item8;

        myArray = array;
        myCount = 9;
      }

      public ListOfArray(T[] array, int count)
      {
        myArray = array;
        myCount = count;
      }

      public override int Capacity => myArray.Length;

      public override ref T GetItemNoRangeCheck(int index)
      {
        return ref myArray[index];
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        throw new NotImplementedException();
        // todo: extend arrays
      }

      public override Builder<T> Clone()
      {
        var newArray = new T[myArray.Length];
        Array.Copy(myArray, newArray, myCount);
        return new ListOfArray<T>(newArray, myCount);
      }

      public override T Current => myArray[myCount - myCountAndIterationData];

      public override int IndexOf(T item)
      {
        throw new NotImplementedException();
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        throw new NotImplementedException();
      }

      public override T this[int index]
      {
        get => throw new NotImplementedException();
      }
    }

    // todo: can we implement this?
    internal sealed class ListOfRefArray<T> : Builder<T>
      where T : class
    {
      private struct Element
      {
        public T Value;
      }

      private Element[] myArray;
      private int myCount;

      public override int Capacity => myArray.Length;

      public override ref T GetItemNoRangeCheck(int index)
      {
        if ((uint) index <= (uint) myCount) ThrowOutOfRange();

        return ref myArray[index].Value;
      }

      public override void Append(in T value, ref Builder<T> self)
      {
        throw new NotImplementedException();
      }

      public override Builder<T> Clone()
      {
        throw new NotImplementedException();
      }

      public override int IndexOf(T item)
      {
        throw new NotImplementedException();
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        throw new NotImplementedException();
      }

      public override T Current { get; }

      public override T this[int index]
      {
        get => throw new NotImplementedException();
      }
    }

    internal sealed class BuilderDebugView<T>
    {
      public BuilderDebugView([NotNull] Builder<T> builder)
      {
        var array = new T[builder.Count];
        builder.CopyTo(array, arrayIndex: 0);
        Items = array;
      }

      [NotNull] public T[] Items { get; }
    }
  }
}
