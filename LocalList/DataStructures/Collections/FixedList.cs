using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;

namespace JetBrains.Util.DataStructures.Collections
{
  // todo: test enumeration/index access speed of T[] vs 8 fields

  [PublicAPI]
  public static class FixedList
  {
    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T item)
      => new ListOf1<T>(item);

    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T first, T second)
      => new ListOf2<T>(in first, in second);

    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T first, T second, T third)
      => new ListOf3<T>(in first, in second, in third);

    [NotNull, Pure]
    public static IReadOnlyList<T> Of<T>(T first, T second, T third, T fourth)
      => new ListOf4<T>(in first, in second, in third, fourth);

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

    [DebuggerTypeProxy(typeof(BuilderDebugView<>))]
    internal abstract class Builder<T> : IReadOnlyList<T>, IList<T>
    {
      // note: this must be of `int` type to use `Interlocked.CompareExchange()`
      internal int CountAndIterationData;

      internal bool IsFrozen => CountAndIterationData >= 0;

      protected const int NotFrozenBit = 1 << 31;

      public abstract int Count { get; }
      public abstract int Capacity { get; }

      [Pure]
      public abstract ref T ItemRefNoRangeCheck(int index);

      public abstract void Append(in T newItem, int count, [NotNull] ref Builder<T> self);

      [NotNull, Pure]
      public abstract Builder<T> Clone(int count);

      [CanBeNull]
      public abstract Builder<T> TrimExcess(int count, bool clone);

      public virtual void CopyToImpl([NotNull] T[] array, int startIndex, int count)
      {
        for (var index = 0; index < count; index++)
        {
          array[startIndex + index] = ItemRefNoRangeCheck(index);
        }
      }

      public virtual void CopyToImpl([NotNull] Builder<T> other, int startIndex, int count)
      {
        Debug.Assert(!IsFrozen);

        other.CountAndIterationData = CountAndIterationData;

        for (var index = 0; index < count; index++)
        {
          other.ItemRefNoRangeCheck(startIndex + index) = ItemRefNoRangeCheck(index);
        }
      }

      [ItemCanBeNull, Pure]
      public virtual T[] TryGetInternalArray() => null;

      #region Read access

      public abstract T this[int index] { get; set; }

      public int IndexOf(T item)
      {
        Debug.Assert(IsFrozen);

        return IndexOf(item, Count);
      }

      [Pure]
      public virtual int IndexOf(T item, int count)
      {
        for (var index = 0; index < count; index++)
        {
          var currentItem = ItemRefNoRangeCheck(index);
          if (EqualityComparer<T>.Default.Equals(item, currentItem)) return index;
        }

        return -1;
      }

      public bool Contains(T item)
      {
        return IndexOf(item, Count) >= 0;
      }

      public void CopyTo(T[] array, int arrayIndex)
      {
        if (array == null)
          throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0)
          throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        var count = Count;
        if (arrayIndex + count > array.Length)
          throw new ArgumentException("The number of items is greater than the available array space");

        CopyToImpl(array, arrayIndex, count);
      }

      #endregion
      #region Write access

      bool ICollection<T>.IsReadOnly => true;

      void ICollection<T>.Add(T item) => throw new CollectionReadOnlyException();
      void IList<T>.Insert(int index, T item) => throw new CollectionReadOnlyException();
      void IList<T>.RemoveAt(int index) => throw new CollectionReadOnlyException();
      bool ICollection<T>.Remove(T item) => throw new CollectionReadOnlyException();
      void ICollection<T>.Clear() => throw new CollectionReadOnlyException();

      #endregion

      public abstract IEnumerator<T> GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>) this).GetEnumerator();

      [ContractAnnotation("=> halt")]
      [MethodImpl(MethodImplOptions.NoInlining)]
      protected static void ThrowOutOfRange()
      {
        // ReSharper disable once NotResolvedInText
        throw new ArgumentOutOfRangeException(
          "index", "Index should be non-negative and less than Count");
      }

      public abstract void Clear(int count);

      public abstract void RemoveAt(int indexToRemove, int count);

      public void ModifyVersion()
      {
        Debug.Assert(!IsFrozen);

        CountAndIterationData = (CountAndIterationData + 1) | NotFrozenBit;
      }

      public abstract void Freeze(int count);
    }

    internal abstract class FixedBuilder<T> : Builder<T>, IEnumerator<T>
    {
      protected FixedBuilder()
      {
        // not frozen, count = 0, version = 0xFFFF
        CountAndIterationData = NotFrozenBit | IteratorOrVersionMask;
      }

      protected FixedBuilder(int count)
      {
        Debug.Assert(count > 0); // use 'EmptyList<T>.Instance' instead
        Debug.Assert(count <= MaxCount);

        // frozen, count is set, before GetEnumerator
        CountAndIterationData = (int) ((uint) count << CountShift) | BeforeGetEnumerator;
      }

      protected int ShortCount
      {
        get
        {
          Debug.Assert(IsFrozen);

          return (int) ((uint) CountAndIterationData << 1 >> CountShift + 1);
        }
      }

      private const int CountShift = 16;

      private const int MaxCount = (int) (uint.MaxValue >> (CountShift + 1));

      protected const int IteratorOrVersionMask = (1 << CountShift) - 1;

      private const int BeforeGetEnumerator = IteratorOrVersionMask - 1;
      private const int BeforeFirstElement = IteratorOrVersionMask;

      public sealed override int Count => ShortCount;

      #region Frozen enumeration

      public override IEnumerator<T> GetEnumerator()
      {
        Debug.Assert(IsFrozen);
        Debug.Assert(ShortCount > 0);

        var data = CountAndIterationData & ~IteratorOrVersionMask;
        var beforeGetEnumerator = data | BeforeGetEnumerator;
        var beforeFirstElement = data | BeforeFirstElement;

        if (beforeGetEnumerator == Interlocked.CompareExchange(
              location1: ref CountAndIterationData,
              value: beforeFirstElement,
              comparand: beforeGetEnumerator))
        {
          return this;
        }

        return new Enumerator(this);
      }

      bool IEnumerator.MoveNext()
      {
        Debug.Assert(IsFrozen);

        var newIterator = (CountAndIterationData + 1) & IteratorOrVersionMask;
        if (newIterator == (CountAndIterationData >> CountShift)) return false;

        CountAndIterationData = CountAndIterationData & ~IteratorOrVersionMask | newIterator;
        return true;
      }

      void IDisposable.Dispose()
      {
        Interlocked.Exchange(
          location1: ref CountAndIterationData,
          value: CountAndIterationData & ~IteratorOrVersionMask | BeforeGetEnumerator);
      }

      void IEnumerator.Reset()
      {
        CountAndIterationData = CountAndIterationData & ~IteratorOrVersionMask | BeforeFirstElement;
      }

      private sealed class Enumerator : IEnumerator<T>
      {
        [NotNull] private readonly Builder<T> myBuilder;
        private readonly short myCount;
        private short myIndex;

        public Enumerator([NotNull] FixedBuilder<T> builder)
        {
          Debug.Assert(builder.IsFrozen);

          myBuilder = builder;
          myCount = (short) builder.ShortCount;
          myIndex = -1;
        }

        public bool MoveNext() => ++myIndex < myCount;

        public T Current => myBuilder.ItemRefNoRangeCheck(myIndex);
        object IEnumerator.Current => Current;

        public void Dispose() { }

        public void Reset()
        {
          myIndex = -1;
        }
      }

      public abstract T Current { get; }
      object IEnumerator.Current => Current;

      #endregion

      public sealed override string ToString()
      {
        var builder = new StringBuilder();
        var state = CountAndIterationData & IteratorOrVersionMask;

        builder.Append(IsFrozen ? "FixedList(" : "Builder(");
        builder.Append("Count = ").Append(Count);

        if (IsFrozen)
        {
          if (state != BeforeGetEnumerator)
          {
            builder.Append(", Enumeration = ");

            if (state == BeforeFirstElement)
              builder.Append("<before item 0>");
            else
              builder.Append("<at item ").Append(state).Append('>');
          }
        }
        else
        {
          builder.Append(", Version = 0x").Append(state.ToString("X4"));
        }

        return builder.Append(')').ToString();
      }

      public override void Freeze(int count)
      {
        CountAndIterationData = (count << CountShift) | BeforeGetEnumerator;
      }
    }

    internal sealed class ListOf1<T> : FixedBuilder<T>
    {
      internal T Item0;

      public ListOf1() { }

      public ListOf1(in T item0) : base(count: 1)
      {
        Item0 = item0;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index > (uint) ShortCount) ThrowOutOfRange();

          return Item0;
        }
        set => throw new CollectionReadOnlyException();
      }

      public override int Capacity => 1;

      public override ref T ItemRefNoRangeCheck(int index) => ref Item0;

      public override void Append(in T newItem, int count, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        if (count == 0)
        {
          Item0 = newItem;
        }
        else
        {
          self = new ListOf4<T>
          {
            CountAndIterationData = CountAndIterationData,
            Item0 = Item0,
            Item1 = newItem
          };
        }
      }

      public override Builder<T> Clone(int count)
      {
        Debug.Assert(!IsFrozen);

        return new ListOf1<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0
        };
      }

      public override Builder<T> TrimExcess(int count, bool clone)
      {
        Debug.Assert(!IsFrozen);

        if (count == 0) return null;

        return clone ? Clone(count) : this;
      }

      public override T Current => Item0;

      public override void Clear(int count)
      {
        Debug.Assert(!IsFrozen);

        Item0 = default;
      }

      public override void RemoveAt(int indexToRemove, int count)
      {
        Debug.Assert(!IsFrozen);

        Item0 = default;
      }
    }

    internal sealed class ListOf2<T> : FixedBuilder<T>
    {
      internal T Item0, Item1;

      public ListOf2() { }

      public ListOf2(in T item0, in T item1) : base(count: 2)
      {
        Item0 = item0;
        Item1 = item1;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index >= (uint) ShortCount) ThrowOutOfRange();

          return index == 0 ? Item0 : Item1;
        }
        set => throw new CollectionReadOnlyException();
      }

      public override int Capacity => 2;

      public override ref T ItemRefNoRangeCheck(int index)
      {
        return ref index == 0 ? ref Item0 : ref Item1;
      }

      public override void Append(in T newItem, int count, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: Item0 = newItem; break;
          case 1: Item1 = newItem; break;
          default: self = Enlarge(newItem); break;
        }
      }

      [Pure, NotNull]
      private Builder<T> Enlarge(in T newItem)
      {
        return new ListOf4<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = newItem
        };
      }

      public override Builder<T> Clone(int count)
      {
        Debug.Assert(!IsFrozen);

        return new ListOf2<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1
        };
      }

      public override Builder<T> TrimExcess(int count, bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: return null;
          case 1: return new ListOf1<T> {Item0 = Item0};
          default: return clone ? Clone(count) : this;
        }
      }

      public override T Current
      {
        get
        {
          var index = CountAndIterationData & IteratorOrVersionMask;
          return index == 0 ? Item0 : Item1;
        }
      }

      public override void Clear(int count)
      {
        Debug.Assert(!IsFrozen);

        Item0 = default;
        Item1 = default;
      }

      public override void RemoveAt(int indexToRemove, int count)
      {
        Debug.Assert(!IsFrozen);

        if (indexToRemove < 1) Item0 = Item1;

        Item1 = default;
      }
    }

    internal sealed class ListOf3<T> : FixedBuilder<T>
    {
      internal T Item0, Item1, Item2;

      public ListOf3() { }

      public ListOf3(in T item0, in T item1, in T item2) : base(count: 3)
      {
        Item0 = item0;
        Item1 = item1;
        Item2 = item2;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index >= (uint) ShortCount) ThrowOutOfRange();

          return ItemRefNoRangeCheck(index);
        }
        set => throw new CollectionReadOnlyException();
      }

      public override int Capacity => 3;

      public override ref T ItemRefNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref Item0;
          case 1: return ref Item1;
          default: return ref Item2;
        }
      }

      public override void Append(in T newItem, int count, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: Item0 = newItem; break;
          case 1: Item1 = newItem; break;
          case 2: Item2 = newItem; break;
          default: self = Enlarge(newItem); break;
        }
      }

      [Pure, NotNull]
      private Builder<T> Enlarge(in T newItem)
      {
        return new ListOf8<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2,
          Item3 = newItem
        };
      }

      public override Builder<T> Clone(int count)
      {
        Debug.Assert(!IsFrozen);

        return new ListOf3<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2
        };
      }

      public override Builder<T> TrimExcess(int count, bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: return null;
          case 1: return new ListOf1<T> { Item0 = Item0 };
          case 2: return new ListOf2<T> { Item0 = Item0, Item1 = Item1 };
          default: return clone ? Clone(count) : this;
        }
      }

      public override T Current
      {
        get
        {
          switch (CountAndIterationData & IteratorOrVersionMask)
          {
            case 0: return Item0;
            case 1: return Item1;
            default: return Item2;
          }
        }
      }

      public override void Clear(int count)
      {
        Debug.Assert(!IsFrozen);

        Item0 = default;
        Item1 = default;
        Item2 = default;
      }

      public override void RemoveAt(int indexToRemove, int count)
      {
        Debug.Assert(!IsFrozen);

        if (indexToRemove < 1) Item0 = Item1;
        if (indexToRemove < 2) Item1 = Item2;

        Item2 = default;
      }
    }

    internal sealed class ListOf4<T> : FixedBuilder<T>
    {
      internal T Item0, Item1, Item2, Item3;

      public ListOf4() { }

      public ListOf4(in T item0)
      {
        Item0 = item0;
      }

      public ListOf4(in T item0, in T item1, in T item2, in T item3) : base(count: 4)
      {
        Item0 = item0;
        Item1 = item1;
        Item2 = item2;
        Item3 = item3;
      }

      public override T this[int index]
      {
        get
        {
          if ((uint) index >= (uint) ShortCount) ThrowOutOfRange();

          return ItemRefNoRangeCheck(index);
        }
        set => throw new CollectionReadOnlyException();
      }

      public override int Capacity => 4;

      public override ref T ItemRefNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref Item0;
          case 1: return ref Item1;
          case 2: return ref Item2;
          default: return ref Item3;
        }
      }

      public override void Append(in T newItem, int count, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: Item0 = newItem; break;
          case 1: Item1 = newItem; break;
          case 2: Item2 = newItem; break;
          case 3: Item3 = newItem; break;
          default: self = Enlarge(newItem); break;
        }
      }

      [Pure, NotNull]
      private Builder<T> Enlarge(in T newItem)
      {
        return new ListOf8<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2,
          Item3 = Item3,
          Item4 = newItem
        };
      }

      public override Builder<T> Clone(int count)
      {
        Debug.Assert(!IsFrozen);

        return new ListOf4<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2,
          Item3 = Item3
        };
      }

      public override Builder<T> TrimExcess(int count, bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: return null;
          case 1: return new ListOf1<T> { Item0 = Item0 };
          case 2: return new ListOf2<T> { Item0 = Item0, Item1 = Item1 };
          case 3: return new ListOf3<T> { Item0 = Item0, Item1 = Item1, Item2 = Item2 };
          default: return clone ? Clone(count) : this;
        }
      }

      public override T Current
      {
        get
        {
          switch (CountAndIterationData & IteratorOrVersionMask)
          {
            case 0: return Item0;
            case 1: return Item1;
            case 2: return Item2;
            default: return Item3;
          }
        }
      }

      public override void Clear(int count)
      {
        Debug.Assert(!IsFrozen);

        Item0 = default;
        Item1 = default;
        Item2 = default;
        Item3 = default;
      }

      public override void RemoveAt(int indexToRemove, int count)
      {
        Debug.Assert(!IsFrozen);

        if (indexToRemove < 1) Item0 = Item1;
        if (indexToRemove < 2) Item1 = Item2;
        if (indexToRemove < 3) Item2 = Item3;

        Item3 = default;
      }
    }

    internal sealed class ListOf8<T> : FixedBuilder<T>
    {
      // ReSharper disable MemberCanBePrivate.Global
      internal T Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7;
      // ReSharper restore MemberCanBePrivate.Global

      public override T this[int index]
      {
        get
        {
          if ((uint) index >= (uint) ShortCount) ThrowOutOfRange();

          return ItemRefNoRangeCheck(index);
        }
        set => throw new CollectionReadOnlyException();
      }

      public override int Capacity => 8;

      public override ref T ItemRefNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref Item0;
          case 1: return ref Item1;
          case 2: return ref Item2;
          case 3: return ref Item3;
          case 4: return ref Item4;
          case 5: return ref Item5;
          case 6: return ref Item6;
          default: return ref Item7;
        }
      }

      public override void Append(in T newItem, int count, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: Item0 = newItem; break;
          case 1: Item1 = newItem; break;
          case 2: Item2 = newItem; break;
          case 3: Item3 = newItem; break;
          case 4: Item4 = newItem; break;
          case 5: Item5 = newItem; break;
          case 6: Item6 = newItem; break;
          case 7: Item7 = newItem; break;
          default: self = Resize(in newItem); break;
        }
      }

      [Pure, NotNull]
      private Builder<T> Resize(in T item)
      {
        var array = new T[Capacity * 2];
        array[0] = Item0;
        array[1] = Item1;
        array[2] = Item2;
        array[3] = Item3;
        array[4] = Item4;
        array[5] = Item5;
        array[6] = Item6;
        array[7] = Item7;
        array[8] = item;

        var result = new ListOfArray<T>(array, count: 9);
        result.CountAndIterationData |= NotFrozenBit; // unfreeze
        return result;
      }

      public override Builder<T> Clone(int count)
      {
        Debug.Assert(!IsFrozen);

        return new ListOf8<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2,
          Item3 = Item3,
          Item4 = Item4,
          Item5 = Item5,
          Item6 = Item6,
          Item7 = Item7
        };
      }

      public override Builder<T> TrimExcess(int count, bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (count)
        {
          case 0: return null;
          case 1: return new ListOf1<T> { Item0 = Item0};
          case 2: return new ListOf2<T> { Item0 = Item0, Item1 = Item1 };
          case 3: return new ListOf3<T> { Item0 = Item0, Item1 = Item1, Item2 = Item2 };
          case 4: return new ListOf4<T> { Item0 = Item0, Item1 = Item1, Item2 = Item2, Item3 = Item3 };

          case 5:
          case 6:
          case 7:
          {
            var array = new T[count];
            CopyToImpl(array, startIndex: 0, count);
            return new ListOfArray<T>(array, count) { CountAndIterationData = NotFrozenBit };
          }

          default: return clone ? Clone(count) : this;
        }
      }

      public override T Current
      {
        get
        {
          var index = CountAndIterationData & IteratorOrVersionMask;
          return ItemRefNoRangeCheck(index);
        }
      }

      public override void Clear(int count)
      {
        Debug.Assert(!IsFrozen);

        Item0 = default;
        Item1 = default;
        Item2 = default;
        Item3 = default;
        Item4 = default;
        Item5 = default;
        Item6 = default;
        Item7 = default;
      }

      public override void RemoveAt(int indexToRemove, int count)
      {
        Debug.Assert(!IsFrozen);

        if (indexToRemove < 1) Item0 = Item1;
        if (indexToRemove < 2) Item1 = Item2;
        if (indexToRemove < 3) Item2 = Item3;
        if (indexToRemove < 4) Item3 = Item4;
        if (indexToRemove < 5) Item4 = Item5;
        if (indexToRemove < 6) Item5 = Item6;
        if (indexToRemove < 7) Item6 = Item7;

        Item7 = default;
      }
    }

    internal sealed class ListOfArray<T> : Builder<T>, IEnumerator<T>
    {
      [NotNull] private T[] myArray;
      private int myCount; // use this instead of myCountAndVersionData

      private const int BeforeGetEnumerator = (int) 0x7FFFFFFEu;
      private const int BeforeFirstElement = (int) 0x7FFFFFFFu;

      private const int IteratorOrVersionMask = BeforeFirstElement;

      public ListOfArray(int capacity)
      {
        CountAndIterationData = NotFrozenBit;
        myArray = new T[capacity];
      }

      public ListOfArray([NotNull] T[] array, int count)
      {
        Debug.Assert(array != null);
        Debug.Assert(array.Length > 0);

        CountAndIterationData = BeforeGetEnumerator; // frozen
        myArray = array;
        myCount = count;
      }

      public override int Count => myCount;

      public override int Capacity => myArray.Length;

      public override ref T ItemRefNoRangeCheck(int index) => ref myArray[index];

      public override void Append(in T newItem, int count, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        if (count == myArray.Length)
        {
          var newArray = new T[count * 2];
          Array.Copy(myArray, 0, newArray, 0, count);
          myArray = newArray;
        }

        myArray[count] = newItem;
      }

      public override Builder<T> Clone(int count)
      {
        Debug.Assert(!IsFrozen);

        var newArray = new T[myArray.Length];
        Array.Copy(myArray, newArray, count);

        var result = new ListOfArray<T>(newArray, count);
        result.CountAndIterationData |= NotFrozenBit; // unfreeze
        return result;
      }

      public override Builder<T> TrimExcess(int count, bool clone)
      {
        Debug.Assert(!IsFrozen);

        if (clone && count == myArray.Length)
        {
          return Clone(count);
        }

        switch (count)
        {
          case 0: return null;
          case 1: return new ListOf1<T> { Item0 = myArray[0] };
          case 2: return new ListOf2<T> { Item0 = myArray[0], Item1 = myArray[1] };
          case 3: return new ListOf3<T> { Item0 = myArray[0], Item1 = myArray[1], Item2 = myArray[2] };
          case 4: return new ListOf4<T> { Item0 = myArray[0], Item1 = myArray[1], Item2 = myArray[2], Item3 = myArray[3] };

          default:
          {
            if (count == myArray.Length)
              return clone ? Clone(count) : this;

            var newArray = new T[count];
            Array.Copy(myArray, newArray, count);
            return new ListOfArray<T>(newArray, count) { CountAndIterationData = NotFrozenBit };
          }
        }
      }

      public override int IndexOf(T item, int count)
      {
        return Array.IndexOf(myArray, item, startIndex: 0, count: count);
      }

      public override void RemoveAt(int indexToRemove, int count)
      {
        Debug.Assert(!IsFrozen);

        Array.Copy(
          sourceArray: myArray,
          sourceIndex: indexToRemove + 1,
          destinationArray: myArray,
          destinationIndex: indexToRemove,
          length: count - indexToRemove - 1);

        myArray[count - 1] = default;
      }

      public override void Clear(int count)
      {
        Debug.Assert(!IsFrozen);

        Array.Clear(myArray, index: 0, length: count);
      }

      public override void Freeze(int count)
      {
        myCount = count;
        CountAndIterationData = BeforeGetEnumerator;
      }

      public override void CopyToImpl(T[] array, int startIndex, int count)
      {
        Array.Copy(
          sourceArray: myArray,
          sourceIndex: 0,
          destinationArray: array,
          destinationIndex: startIndex,
          length: count);
      }

      public override void CopyToImpl(Builder<T> other, int startIndex, int count)
      {
        Debug.Assert(!IsFrozen);

        other.CountAndIterationData = CountAndIterationData;

        if (other is ListOfArray<T> otherArray)
        {
          Array.Copy(
            sourceArray: myArray,
            sourceIndex: 0,
            destinationArray: otherArray.myArray,
            destinationIndex: startIndex,
            length: count);
        }
        else
        {
          for (var index = 0; index < count; index++)
          {
            other.ItemRefNoRangeCheck(startIndex + index) = myArray[index];
          }
        }
      }

      public override T[] TryGetInternalArray() => myArray;

      public override T this[int index]
      {
        get
        {
          Debug.Assert(IsFrozen);

          if ((uint) index >= (uint) myCount) ThrowOutOfRange();

          return myArray[index];
        }
        set => throw new CollectionReadOnlyException();
      }

      #region Frozen enumeration

      public override IEnumerator<T> GetEnumerator()
      {
        Debug.Assert(IsFrozen);
        Debug.Assert(myCount > 0);

        if (BeforeGetEnumerator == Interlocked.CompareExchange(
              location1: ref CountAndIterationData, value: BeforeFirstElement, comparand: BeforeGetEnumerator))
        {
          return this;
        }

        return new Enumerator(this);
      }

      bool IEnumerator.MoveNext()
      {
        CountAndIterationData = (CountAndIterationData + 1) & ~NotFrozenBit;
        return CountAndIterationData < myCount;
      }

      void IDisposable.Dispose()
      {
        Interlocked.Exchange(location1: ref CountAndIterationData, value: BeforeGetEnumerator);
      }

      void IEnumerator.Reset()
      {
        CountAndIterationData = BeforeFirstElement;
      }

      T IEnumerator<T>.Current => myArray[CountAndIterationData];
      object IEnumerator.Current => myArray[CountAndIterationData];

      private sealed class Enumerator : IEnumerator<T>
      {
        [NotNull] private readonly T[] myArray;
        private readonly int myCount;
        private int myIndex;

        public Enumerator(ListOfArray<T> builder)
        {
          Debug.Assert(builder.IsFrozen);

          myArray = builder.myArray;
          myCount = builder.myCount;
          myIndex = -1;
        }

        public bool MoveNext() => ++myIndex < myCount;

        public T Current => myArray[myIndex];
        object IEnumerator.Current => Current;

        public void Dispose() { }

        public void Reset()
        {
          myIndex = -1;
        }
      }

      #endregion

      public override string ToString()
      {
        var builder = new StringBuilder();
        var state = CountAndIterationData & IteratorOrVersionMask;

        builder.Append(IsFrozen ? "FixedList(" : "Builder(");
        builder.Append("Count = ").Append(Count);

        if (IsFrozen)
        {
          if (state != BeforeGetEnumerator)
          {
            builder.Append(", Enumeration = ");

            if (state == BeforeFirstElement)
              builder.Append("<before item 0>");
            else
              builder.Append("<at item ").Append(state).Append('>');
          }
        }
        else
        {
          builder.Append(", Version = 0x").Append(state.ToString("X4"));
        }

        return builder.Append(')').ToString();
      }
    }

    internal sealed class BuilderDebugView<T>
    {
      public BuilderDebugView([NotNull] Builder<T> builder)
      {
        var array = new T[builder.Count];
        builder.CopyToImpl(array, startIndex: 0, builder.Capacity);
        Items = array;
      }

      // ReSharper disable once MemberCanBePrivate.Global
      // ReSharper disable once UnusedAutoPropertyAccessor.Global
      [NotNull] public T[] Items { get; }
    }
  }
}