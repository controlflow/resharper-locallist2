using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace JetBrains.Util.DataStructures.Collections
{
  // todo: test enumeration/index access speed of T[] vs 8 fields
  // todo: Count problem!

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

    // todo: value-type public mutable enumerator
    [DebuggerTypeProxy(typeof(BuilderDebugView<>))]
    internal abstract class Builder<T> : IReadOnlyList<T>, IEnumerator<T>, IList<T>
    {
      // [    15 bits    ] [1 bit] [        16 bits        ]
      //         |            |                |
      //         |            |                +-- version OR iteration data
      //         |            |
      //         |            +-- frozen or not (1 means frozen)
      //         |
      //         +-- items count, unsigned
      //
      // note: this must be of `int` type to use `Interlocked.CompareExchange()`
      internal int CountAndIterationData;

      protected bool IsFrozen => (CountAndIterationData & NotFrozenBit) != 0;

      public int Count => (int) ((uint) CountAndIterationData >> CountShift);

      protected const int FrozenBitShift = 16;
      protected const int CountShift = FrozenBitShift + 1;

      protected const int NotFrozenBit = 1 << (CountShift - 1);

      protected const int MaxCount = (int) (uint.MaxValue >> CountShift);

      protected const int NotFrozenCount0 = NotFrozenBit;
      protected const int NotFrozenCount1 = (1 << CountShift) | NotFrozenBit;
      protected const int NotFrozenCount2 = (2 << CountShift) | NotFrozenBit;
      protected const int NotFrozenCount3 = (3 << CountShift) | NotFrozenBit;
      protected const int NotFrozenCount4 = (4 << CountShift) | NotFrozenBit;
      protected const int NotFrozenCount5 = (5 << CountShift) | NotFrozenBit;

      // iterator/version data is stored in first 16 bits
      // but we include count there as well, because why not
      // todo: possible store in short?
      protected int Version => CountAndIterationData;

      protected const int IteratorOrVersionMask = (1 << FrozenBitShift) - 1;
      protected const int VersionAndCountIncrement = (1 << CountShift) + 1;

      // todo: clarify
      protected const int ReadyForGetEnumerator = IteratorOrVersionMask;

      protected Builder()
      {
        CountAndIterationData = NotFrozenBit; // not frozen, count = 0, version = 0
      }

      protected Builder(int count)
      {
        Debug.Assert(count > 0); // use 'EmptyList<T>.Instance' instead
        Debug.Assert(count <= MaxCount);

        CountAndIterationData = (int) ((uint) count << CountShift) | ReadyForGetEnumerator; // frozen, count is set
      }

      public abstract int Capacity { get; }

      [Pure]
      public abstract ref T GetItemNoRangeCheck(int index);

      public abstract void Append(in T newItem, ref Builder<T> self);

      [NotNull, Pure] public abstract Builder<T> Clone();

      [CanBeNull, Pure] public abstract Builder<T> TrimExcess(bool clone);

      protected abstract void CopyToImpl([NotNull] T[] array, int arrayIndex);

      internal struct BuilderEnumerator
      {
        // todo: version checking
      }

      #region Frozen enumeration

      public IEnumerator<T> GetEnumerator()
      {
        Debug.Assert(IsFrozen);

        var data = CountAndIterationData;
        var count = data >> CountShift;
        var expected = data | ReadyForGetEnumerator;

        if (expected == Interlocked.CompareExchange(
              location1: ref CountAndIterationData, value: data | count, comparand: expected))
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

        var newData = CountAndIterationData - 1;
        if (newData < 0) return false;

        CountAndIterationData = newData;
        return true;
      }

      void IDisposable.Dispose()
      {
        Interlocked.Exchange(
          ref CountAndIterationData, value: CountAndIterationData | ReadyForGetEnumerator);
      }

      void IEnumerator.Reset()
      {
        CountAndIterationData |= ~IteratorOrVersionMask;
      }

      public abstract T Current { get; }
      object IEnumerator.Current => Current;

      #endregion
      #region Read access

      public abstract T this[int index] { get; set; }

      // todo: make use of .Count in both of those methods
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

      [ContractAnnotation("=> halt")]
      protected static void ThrowOutOfRange()
      {
        // ReSharper disable once NotResolvedInText
        throw new ArgumentOutOfRangeException("index", "Index should be non-negative and less than Count");
      }

      [ContractAnnotation("=> halt")]
      protected static void ThrowFrozen()
      {
        throw new CollectionReadOnlyException();
      }

      public sealed override string ToString()
      {
        return $"{nameof(FixedList)}(Count = {Count.ToString()})";
      }
    }

    internal sealed class ListOf1<T> : Builder<T>
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
          if ((uint) index >= (uint) Count) ThrowOutOfRange();

          return Item0;
        }
        set
        {
          if (IsFrozen) ThrowFrozen();
          if ((uint) index >= Count) ThrowOutOfRange();

          Item0 = value;
        }
      }

      public override int Capacity => 1;

      public override ref T GetItemNoRangeCheck(int index) => ref Item0;

      public override void Append(in T newItem, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        var newData = CountAndIterationData += VersionAndCountIncrement;
        if (newData >> CountShift == 1)
        {
          Item0 = newItem;
        }
        else
        {
          self = new ListOf4<T>
          {
            CountAndIterationData = NotFrozenCount2,
            Item0 = Item0,
            Item2 = newItem
          };
        }
      }

      public override Builder<T> Clone()
      {
        Debug.Assert(!IsFrozen); // there should be no need to clone the immutable data

        return new ListOf1<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0
        };
      }

      public override Builder<T> TrimExcess(bool clone)
      {
        Debug.Assert(!IsFrozen); // mutable operation

        if (Count == 0) return null;

        return clone ? Clone() : this;
      }

      public override T Current => Item0;

      public override int IndexOf(T item)
      {
        if (Count == 1 && EqualityComparer<T>.Default.Equals(item, Item0)) return 0;

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        if (Count == 1) array[arrayIndex] = Item0;
      }
    }

    internal sealed class ListOf2<T> : Builder<T>
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
          if ((uint) index >= (uint) Count) ThrowOutOfRange();

          return index == 0 ? Item0 : Item1;
        }
        set
        {
          if (IsFrozen) ThrowFrozen();
          if ((uint) index >= Count) ThrowOutOfRange();

          if (index == 0)
            Item0 = value;
          else
            Item1 = value;
        }
      }

      public override int Capacity => 2;

      public override ref T GetItemNoRangeCheck(int index)
      {
        return ref index == 0 ? ref Item0 : ref Item1;
      }

      public override void Append(in T newItem, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        var newData = CountAndIterationData += VersionAndCountIncrement;

        switch (newData >> CountShift)
        {
          case 1: Item0 = newItem; break;
          case 2: Item1 = newItem; break;
          default: self = Resize(newItem); break;
        }
      }

      private Builder<T> Resize(in T newItem)
      {
        return new ListOf4<T>
        {
          CountAndIterationData = NotFrozenCount3,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = newItem
        };
      }

      public override Builder<T> Clone()
      {
        Debug.Assert(!IsFrozen);

        return new ListOf2<T>
        {
          CountAndIterationData = CountAndIterationData,
          Item0 = Item0,
          Item1 = Item1
        };
      }

      public override Builder<T> TrimExcess(bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (Count)
        {
          case 0: return null;
          case 1: return new ListOf1<T> {CountAndIterationData = NotFrozenCount1, Item0 = Item0};
          default: return clone ? Clone() : this;
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

      public override int IndexOf(T item)
      {
        var count = Count;
        if (count > 0)
        {
          if (EqualityComparer<T>.Default.Equals(item, Item0)) return 0;

          if (count > 1)
          {
            if (EqualityComparer<T>.Default.Equals(item, Item1)) return 1;
          }
        }

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        var count = Count;
        if (count > 0)
        {
          array[arrayIndex++] = Item0;

          if (count > 1)
          {
            array[arrayIndex] = Item1;
          }
        }
      }
    }

    internal sealed class ListOf3<T> : Builder<T>
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
          if ((uint) index > (uint) Count) ThrowOutOfRange();

          switch (index)
          {
            case 0: return Item0;
            case 1: return Item1;
            default: return Item2;
          }
        }
        set
        {
          if (IsFrozen) ThrowFrozen();
          if ((uint) index >= Count) ThrowOutOfRange();

          switch (index)
          {
            case 0: Item0 = value; break;
            case 1: Item1 = value; break;
            default: Item2 = value; break;
          }
        }
      }

      public override int Capacity => 3;

      public override ref T GetItemNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref Item0;
          case 1: return ref Item1;
          default: return ref Item2;
        }
      }

      public override void Append(in T newItem, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        var newData = CountAndIterationData += VersionAndCountIncrement;

        switch (newData >> CountShift)
        {
          case 1: Item0 = newItem; break;
          case 2: Item1 = newItem; break;
          case 3: Item2 = newItem; break;
          default: self = Resize(newItem); break;
        }
      }

      private Builder<T> Resize(in T newItem)
      {
        return new ListOf8<T>
        {
          CountAndIterationData = NotFrozenCount4,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2,
          Item3 = newItem
        };
      }

      public override Builder<T> Clone()
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

      public override Builder<T> TrimExcess(bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (Count)
        {
          case 0:
            return null;

          case 1:
            return new ListOf1<T>
            {
              CountAndIterationData = NotFrozenCount1,
              Item0 = Item0
            };

          case 2:
            return new ListOf2<T>
            {
              CountAndIterationData = NotFrozenCount2,
              Item0 = Item0,
              Item1 = Item1
            };

          default:
            return clone ? Clone() : this;
        }
      }

      public override T Current
      {
        get
        {
          switch (CountAndIterationData & IteratorOrVersionMask)
          {
            case 0: return Item2;
            case 1: return Item1;
            default: return Item0;
          }
        }
      }

      public override int IndexOf(T item)
      {
        var count = Count;
        if (count > 0)
        {
          if (EqualityComparer<T>.Default.Equals(item, Item0)) return 0;

          if (count > 1)
          {
            if (EqualityComparer<T>.Default.Equals(item, Item1)) return 1;

            if (count > 2)
            {
              if (EqualityComparer<T>.Default.Equals(item, Item2)) return 2;
            }
          }
        }

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        var count = Count;
        if (count > 0)
        {
          array[arrayIndex++] = Item0;

          if (count > 1)
          {
            array[arrayIndex++] = Item1;

            if (count > 2)
            {
              array[arrayIndex] = Item2;
            }
          }
        }
      }
    }

    internal sealed class ListOf4<T> : Builder<T>
    {
      internal T Item0, Item1, Item2, Item3;

      public ListOf4() { }

      public ListOf4(in T item0)
      {
        CountAndIterationData = NotFrozenCount1;
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
          if ((uint) index > (uint) Count) ThrowOutOfRange();

          switch (index)
          {
            case 0: return Item0;
            case 1: return Item1;
            case 2: return Item2;
            default: return Item3;
          }
        }
        set
        {
          if (IsFrozen) ThrowFrozen();
          if ((uint) index >= Count) ThrowOutOfRange();

          GetItemNoRangeCheck(index) = value;
        }
      }

      public override int Capacity => 4;

      public override ref T GetItemNoRangeCheck(int index)
      {
        switch (index)
        {
          case 0: return ref Item0;
          case 1: return ref Item1;
          case 2: return ref Item2;
          default: return ref Item3;
        }
      }

      public override void Append(in T newItem, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        var newData = CountAndIterationData += VersionAndCountIncrement;
        switch (newData >> CountShift)
        {
          case 1: Item0 = newItem; break;
          case 2: Item1 = newItem; break;
          case 3: Item2 = newItem; break;
          case 4: Item3 = newItem; break;
          default: self = Resize(newItem); break;
        }
      }

      private Builder<T> Resize(in T newItem)
      {
        return new ListOf8<T>
        {
          CountAndIterationData = NotFrozenCount5,
          Item0 = Item0,
          Item1 = Item1,
          Item2 = Item2,
          Item3 = Item3,
          Item4 = newItem
        };
      }

      public override Builder<T> Clone()
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

      public override Builder<T> TrimExcess(bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (Count)
        {
          case 0:
            return null;

          case 1:
            return new ListOf1<T>
            {
              CountAndIterationData = NotFrozenCount1,
              Item0 = Item0
            };

          case 2:
            return new ListOf2<T>
            {
              CountAndIterationData = NotFrozenCount2,
              Item0 = Item0,
              Item1 = Item1
            };

          case 3:
            return new ListOf3<T>
            {
              CountAndIterationData = NotFrozenCount3,
              Item0 = Item0,
              Item1 = Item1,
              Item2 = Item2
            };

          default:
            return clone ? Clone() : this;
        }
      }

      public override T Current
      {
        get
        {
          switch (CountAndIterationData & IteratorOrVersionMask)
          {
            case 3: return Item0;
            case 2: return Item1;
            case 1: return Item2;
            default: return Item3;
          }
        }
      }

      public override int IndexOf(T item)
      {
        var count = Count;
        if (count > 0)
        {
          if (EqualityComparer<T>.Default.Equals(item, Item0)) return 0;

          if (count > 1)
          {
            if (EqualityComparer<T>.Default.Equals(item, Item1)) return 1;

            if (count > 2)
            {
              if (EqualityComparer<T>.Default.Equals(item, Item2)) return 2;

              if (count > 3)
              {
                if (EqualityComparer<T>.Default.Equals(item, Item3)) return 3;
              }
            }
          }
        }

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        var count = Count;
        if (count > 0)
        {
          array[arrayIndex++] = Item0;

          if (count > 1)
          {
            array[arrayIndex++] = Item1;

            if (count > 2)
            {
              array[arrayIndex++] = Item2;

              if (count > 3)
              {
                array[arrayIndex] = Item3;
              }
            }
          }
        }
      }
    }

    internal sealed class ListOf8<T> : Builder<T>
    {
      // ReSharper disable MemberCanBePrivate.Global
      internal T Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7;
      // ReSharper restore MemberCanBePrivate.Global

      public override T this[int index]
      {
        get
        {
          if ((uint) index > (uint) Count) ThrowOutOfRange();

          switch (index)
          {
            case 0: return Item0;
            case 1: return Item1;
            case 2: return Item2;
            case 3: return Item3;
            case 4: return Item4;
            case 5: return Item5;
            case 6: return Item6;
            default: return Item7;
          }
        }
        set
        {
          if (IsFrozen) ThrowFrozen();
          if ((uint) index >= Count) ThrowOutOfRange();

          GetItemNoRangeCheck(index) = value;
        }
      }

      public override int Capacity => 8;

      public override ref T GetItemNoRangeCheck(int index)
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

      public override void Append(in T newItem, ref Builder<T> self)
      {
        Debug.Assert(!IsFrozen);

        var newData = CountAndIterationData += VersionAndCountIncrement;

        switch (newData >> CountShift)
        {
          case 1: Item0 = newItem; break;
          case 2: Item1 = newItem; break;
          case 3: Item2 = newItem; break;
          case 4: Item3 = newItem; break;
          case 5: Item4 = newItem; break;
          case 6: Item5 = newItem; break;
          case 7: Item6 = newItem; break;
          case 8: Item7 = newItem; break;
          default: self = Resize(in newItem); break;
        }
      }

      private Builder<T> Resize(in T item)
      {
        var array = new T[16];
        array[0] = Item0;
        array[1] = Item1;
        array[2] = Item2;
        array[3] = Item3;
        array[4] = Item4;
        array[5] = Item5;
        array[6] = Item6;
        array[7] = Item7;
        array[8] = item;
        return new ListOfArray<T>(array, count: 8);
      }

      public override Builder<T> Clone()
      {
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

      public override Builder<T> TrimExcess(bool clone)
      {
        Debug.Assert(!IsFrozen);

        switch (Count)
        {
          case 0:
            return null;

          case 1:
            return new ListOf1<T>
            {
              CountAndIterationData = NotFrozenCount1,
              Item0 = Item0
            };

          case 2:
            return new ListOf2<T>
            {
              CountAndIterationData = NotFrozenCount2,
              Item0 = Item0,
              Item1 = Item1
            };

          case 3:
            return new ListOf3<T>
            {
              CountAndIterationData = NotFrozenCount3,
              Item0 = Item0,
              Item1 = Item1,
              Item2 = Item2
            };

          case 4:
            return new ListOf4<T>
            {
              CountAndIterationData = NotFrozenCount4,
              Item0 = Item0,
              Item1 = Item1,
              Item2 = Item2,
              Item3 = Item3
            };

          // todo: allocate as an array?
          // case 5:
          // case 6:
          // case 7:
          //   return new ListOfArray<T>();

          default:
            return clone ? Clone() : this;
        }
      }

      public override T Current
      {
        get
        {
          switch (CountAndIterationData & IteratorOrVersionMask)
          {
            case 7: return Item0;
            case 6: return Item1;
            case 5: return Item2;
            case 4: return Item3;
            case 3: return Item4;
            case 2: return Item5;
            case 1: return Item6;
            default: return Item7;
          }
        }
      }

      public override int IndexOf(T item)
      {
        var count = Count;
        if (count > 0)
        {
          if (EqualityComparer<T>.Default.Equals(item, Item0)) return 0;

          if (count > 1)
          {
            if (EqualityComparer<T>.Default.Equals(item, Item1)) return 1;

            if (count > 2)
            {
              if (EqualityComparer<T>.Default.Equals(item, Item2)) return 2;

              if (count > 3)
              {
                if (EqualityComparer<T>.Default.Equals(item, Item3)) return 3;

                if (count > 4)
                {
                  if (EqualityComparer<T>.Default.Equals(item, Item4)) return 4;

                  if (count > 5)
                  {
                    if (EqualityComparer<T>.Default.Equals(item, Item5)) return 5;

                    if (count > 6)
                    {
                      if (EqualityComparer<T>.Default.Equals(item, Item6)) return 6;

                      if (count > 7)
                      {
                        if (EqualityComparer<T>.Default.Equals(item, Item7)) return 7;
                      }
                    }
                  }
                }
              }
            }
          }
        }

        return -1;
      }

      protected override void CopyToImpl(T[] array, int arrayIndex)
      {
        var count = Count;
        if (count > 0)
        {
          array[arrayIndex++] = Item0;

          if (count > 1)
          {
            array[arrayIndex++] = Item1;

            if (count > 2)
            {
              array[arrayIndex++] = Item2;

              if (count > 3)
              {
                array[arrayIndex++] = Item3;

                if (count > 4)
                {
                  array[arrayIndex++] = Item4;

                  if (count > 5)
                  {
                    array[arrayIndex++] = Item5;

                    if (count > 6)
                    {
                      array[arrayIndex++] = Item6;

                      if (count > 7)
                      {
                        array[arrayIndex] = Item7;
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }

    internal sealed class ListOfArray<T> : Builder<T>
    {
      [NotNull] private T[] myArray;
      private int myCount; // use this instead of myCountAndVersionData

      public ListOfArray([NotNull] T[] array, int count)
      {
        Debug.Assert(array != null);

        myArray = array;
        myCount = count;
      }

      [Obsolete("Use 'myCount' instead", error: true)]
      [UsedImplicitly]
      public new int Count => -1;

      public override int Capacity => myArray.Length;

      public override ref T GetItemNoRangeCheck(int index) => ref myArray[index];

      public override void Append(in T newItem, ref Builder<T> self)
      {
        if (myCount == myArray.Length)
        {
          var newArray = new T[myCount * 2];
          Array.Copy(myArray, 0, newArray, 0, myCount);
          myArray = newArray;
        }

        myArray[myCount++] = newItem;
        CountAndIterationData++;
      }

      public override Builder<T> Clone()
      {
        Debug.Assert(!IsFrozen);

        var newArray = new T[myArray.Length];
        Array.Copy(myArray, newArray, myCount);
        return new ListOfArray<T>(newArray, myCount);
      }

      public override Builder<T> TrimExcess(bool clone)
      {
        Debug.Assert(!IsFrozen);

        if (clone && myCount == myArray.Length)
        {
          return Clone();
        }

        switch (myCount)
        {
          case 0:
            return null;

          case 1:
            return new ListOf1<T>
            {
              CountAndIterationData = NotFrozenCount1,
              Item0 = myArray[0]
            };

          case 2:
            return new ListOf2<T>
            {
              CountAndIterationData = NotFrozenCount2,
              Item0 = myArray[0],
              Item1 = myArray[1]
            };

          case 3:
            return new ListOf3<T>
            {
              CountAndIterationData = NotFrozenCount3,
              Item0 = myArray[0],
              Item1 = myArray[1],
              Item2 = myArray[2]
            };

          case 4:
            return new ListOf4<T>
            {
              CountAndIterationData = NotFrozenCount4,
              Item0 = myArray[0],
              Item1 = myArray[1],
              Item2 = myArray[2],
              Item3 = myArray[3]
            };

          default:
            return clone ? Clone() : this;
        }


        var newArray = new T[myArray.Length];
        Array.Copy(myArray, newArray, myCount);
        return new ListOfArray<T>(newArray, myCount);
      }

      public override T Current => myArray[myCount - CountAndIterationData];

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
        get
        {
          if ((uint) index >= (uint) myCount) ThrowOutOfRange();

          return myArray[index];
        }
        set
        {
          if ((uint) index >= (uint) myCount) ThrowOutOfRange();
          if (IsFrozen) ThrowFrozen();

          myArray[index] = value;
        }
      }
    }

    // todo: can we implement this?
    internal sealed class ListOfRefArray<T> : Builder<T>
      where T : class
    {
      private struct Element : IEquatable<Element>
      {
        public T Value;

        public bool Equals(Element other)
        {
          return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj) => throw new InvalidOperationException();
        public override int GetHashCode() => throw new InvalidOperationException();
      }

      private Element[] myArray;
      private int myCount;

      public override int Capacity => myArray.Length;

      public override ref T GetItemNoRangeCheck(int index)
      {
        if ((uint) index <= (uint) myCount) ThrowOutOfRange();

        return ref myArray[index].Value;
      }

      public override void Append(in T newItem, ref Builder<T> self)
      {
        throw new NotImplementedException();
      }

      public override Builder<T> Clone()
      {
        throw new NotImplementedException();
      }

      public override Builder<T> TrimExcess(bool clone)
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

      public override T Current => throw new NotImplementedException();

      public override T this[int index]
      {
        get { throw new NotImplementedException(); }
        set { throw new NotImplementedException(); }
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

      // ReSharper disable once MemberCanBePrivate.Global
      // ReSharper disable once UnusedAutoPropertyAccessor.Global
      [NotNull] public T[] Items { get; }
    }
  }
}
