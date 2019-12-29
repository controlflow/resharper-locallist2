using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Util.DataStructures.Collections;
// ReSharper disable MergeConditionalExpression

// ReSharper disable once CheckNamespace
namespace JetBrains.Util
{
  // NOTE: [downside] may allocate array instead of returning internal one
  // todo: check with 65636 'byte' items
  // todo: check indexer
  // todo: check enumeration
  // todo: annotate methods readonly
  // todo: must modify version on enlarge

  /// <summary>
  /// Represents collection of items that doesn't create heap objects unless items are added
  /// List is presented as array with capacity increasing as fibonacci numbers.
  /// To obtain <c>IList</c> invoke <c>ResultingList()</c>
  /// </summary>
  [StructLayout(LayoutKind.Auto)]
  [DebuggerDisplay("Count = {" + nameof(Count) + "}")]
  [DebuggerTypeProxy(typeof(LocalList2DebugView<>))]
  [PublicAPI]
  public struct LocalList2<T>
  {
    private const int DefaultFirstSize = 4;

    [CanBeNull] private FixedList.Builder<T> myList;
    private int myCount;


    // todo: to be removed
    [Obsolete] private static T[] myArray;
    [Obsolete] private static int myNextSize;
    [Obsolete] private static int myVersion;

    #region Constructors

    public LocalList2(in LocalList2<T> other, bool preserveCapacity = false)
    {
      myCount = other.myCount;

      var otherList = other.myList;
      if (otherList != null)
      {
        myList = preserveCapacity ? otherList.Clone() : otherList.TrimExcess(clone: true);
      }
      else
      {
        myList = null;
      }
    }

    public LocalList2(int capacity, bool forceUseArray = false)
    {
      if (capacity < 0)
        throw new ArgumentOutOfRangeException(nameof(capacity));

      Debug.Assert(capacity >= 0, "capacity >= 0");

      myCount = 0;

      if (forceUseArray && capacity > 0)
      {
        myList = new FixedList.ListOfArray<T>(capacity);
      }
      else
      {
        switch (capacity)
        {
          case 0: myList = null; break;
          case 1: myList = new FixedList.ListOf1<T>(); break;
          case 2: myList = new FixedList.ListOf2<T>(); break;
          case 3: myList = new FixedList.ListOf3<T>(); break;
          case 4: myList = new FixedList.ListOf4<T>(); break;
          case 5:
          case 6:
          case 7:
          case 8: myList = new FixedList.ListOf8<T>(); break;
          default: myList = new FixedList.ListOfArray<T>(capacity); break;
        }
      }
    }

    public LocalList2([NotNull] IEnumerable<T> enumerable)
    {
      myList = null;
      myCount = 0;
      AddRange(enumerable);
    }

    public LocalList2([NotNull] T[] array, bool copyArray = true)
    {
      myCount = 0;

      if (copyArray)
      {
        myList = null;
        AddRange(array);
      }
      else
      {
        // todo: not recommended
        // todo: leave Frozen?
        myList = new FixedList.ListOfArray<T>(array, array.Length);
      }
    }

    #endregion

    /// <summary>
    /// Gets the number of elements contained in the <see cref="LocalList2{T}"/>.
    /// </summary>
    public readonly int Count => myCount;

    public readonly int Capacity => myList == null ? 0 : myList.Capacity;

    public readonly T this[int index]
    {
      get
      {
        if (myList == null) ThrowOutOfRange();
        if (myList.IsFrozen) ThrowResultObtained();

        if ((uint) index > (uint) myCount) ThrowOutOfRange();

        return myList.GetItemNoRangeCheck(index);
      }
      set
      {
        if (myList == null) ThrowOutOfRange();
        if (myList.IsFrozen) ThrowResultObtained();

        if ((uint) index > (uint) myCount) ThrowOutOfRange();

        myList.GetItemNoRangeCheck(index) = value;
        myList.ModifyVersion();
      }
    }

    public void Add(T item)
    {
      if (myList == null)
      {
        myList = new FixedList.ListOf4<T>(in item);
      }
      else
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion();
        myList.Append(in item, myCount, ref myList);
      }

      myCount++;
    }

    [Pure]
    public readonly int IndexOf(T item)
    {
      if (myList == null) return -1;
      if (myList.IsFrozen) ThrowResultObtained();

      return myList.IndexOf(item, myCount);
    }

    [Pure]
    public readonly bool Contains(T item)
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      var index = myList.IndexOf(item, myCount);
      return index >= 0;
    }

    public bool Remove(T item)
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      var index = myList.IndexOf(item, myCount);
      if (index < 0) return false;

      //myList.RemoveAt(index);
      throw null;
      return true;
    }

    public void Clear()
    {
      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion();
        myList.Clear();
        myCount = 0;
      }
    }

    [Pure, NotNull]
    public IList<T> ResultingList()
    {
      if (myList == null)
      {
        myList = FrozenEmpty;
        return EmptyList<T>.Instance;
      }

      if (myList.IsFrozen) ThrowResultObtained();

      myList.Freeze(myCount);

      return myCount == 0 ? EmptyList<T>.InstanceList : myList;
    }

    [Pure, NotNull]
    public IReadOnlyList<T> ReadOnlyList()
    {
      if (myList == null)
      {
        myList = FrozenEmpty;
        return EmptyList<T>.Instance;
      }

      if (myList.IsFrozen) ThrowResultObtained();

      myList.Freeze(myCount);

      return myCount == 0 ? EmptyList<T>.ReadOnly : myList;
    }

    #region LINQ-like methods

    [Pure]
    public readonly bool Any()
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      return myList.Count > 0;
    }

    [Pure]
    public readonly bool Any([InstantHandle, NotNull] Func<T, bool> predicate)
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      for (int index = 0, count = myList.Count; index < count; index++)
      {
        if (predicate(myList.GetItemNoRangeCheck(index)))
          return true;
      }

      return false;
    }

    [Pure]
    public readonly T Last()
    {
      if (myList == null) ThrowEmpty();
      if (myList.IsFrozen) ThrowResultObtained();

      var count = myList.Count;
      if (count == 0) ThrowEmpty();

      return myList.GetItemNoRangeCheck(count - 1);
    }

    [Pure]
    public readonly T First()
    {
      if (myList == null) ThrowEmpty();
      if (myList.IsFrozen) ThrowResultObtained();

      var count = myList.Count;
      if (count == 0) ThrowEmpty();

      return myList.GetItemNoRangeCheck(0);
    }

    [Pure]
    public readonly T Single()
    {
      if (myList == null) ThrowEmpty();
      if (myList.IsFrozen) ThrowResultObtained();

      var count = myList.Count;
      if (count == 0) ThrowEmpty();
      if (count > 1) ThrowManyItems();

      return myList.GetItemNoRangeCheck(0);
    }

    [CanBeNull]
    public readonly T SingleItem
    {
      get
      {
        if (myList != null)
        {
          if (myList.IsFrozen) ThrowResultObtained();

          // we can avoid .Count check here, since all the list contain at least one slot
          // and there is an invariant of storing `default` in unused data slots
          return myList.GetItemNoRangeCheck(0);
        }

        return default;
      }
    }

    [Pure, CanBeNull]
    public readonly T FirstOrDefault()
    {
      if (myList == null) return default;
      if (myList.IsFrozen) ThrowResultObtained();

      // note: intentionally no .Count checks
      return myList.GetItemNoRangeCheck(0);
    }

    [Pure, CanBeNull]
    public readonly T LastOrDefault()
    {
      if (myList == null) return default;
      if (myList.IsFrozen) ThrowResultObtained();

      var count = Count;
      if (count == 0)
        return default;

      return myList.GetItemNoRangeCheck(count - 1);
    }

    #endregion

    public void AddRange<TSource>([NotNull] IEnumerable<TSource> items)
      where TSource : T
    {
      if (items is ICollection<T> collection)
      {
        AddRange(collection);
        return;
      }

      if (myVersion == -1) ThrowResultObtained();

      var itemsCount = items.TryGetCountFast();
      if (itemsCount > 0)
      {
        EnsureCapacity(myCount + itemsCount);
      }

      foreach (var item in items)
      {
        Add(item);
      }
    }

    public void AddRange([NotNull] IEnumerable<T> items)
    {
      AddRange<T>(items);
    }

    public void AddRange(in LocalList2<T> items)
    {
      //AddRange(items.myArray, items.myCount);
    }

    public void AddRange<TSource>([NotNull] TSource[] items)
      where TSource : T
    {
      AddRange(items, items.Length);
    }

    private void AddRange<TSource>([NotNull] TSource[] items, int length)
      where TSource : T
    {
      if (myVersion == -1) ThrowResultObtained();

      if (length == 0) return;

      EnsureCapacity(myCount + length);
      Array.Copy(items, 0, myArray, myCount, length);
      myCount += length;
    }

    public void AddRange([NotNull] ICollection<T> items)
    {
      if (myVersion == -1) ThrowResultObtained();

      var count = items.Count;
      if (count == 0) return;

      EnsureCapacity(myCount + count);
      items.CopyTo(myArray, myCount);
      myCount += count;
    }

    public void EnsureCapacity(int capacity, bool exact = false)
    {
      var currentSize = Capacity;
      if (capacity <= currentSize) return;

      var newSize = capacity * 2;
      if (exact)
      {
        newSize = capacity;
      }

      var newArray = new T[newSize];

      if (myArray != null)
      {
        Array.Copy(myArray, 0, newArray, 0, myArray.Length);
      }

      myArray = newArray;
    }

    [Pure, NotNull]
    public T[] ToArray()
    {
      if (myVersion == -1) ThrowResultObtained();

      myVersion = -1;

      if (myArray == null | myCount == 0)
        return EmptyArray<T>.Instance;
      if (myArray.Length == myCount)
        return myArray;

      var array = new T[myCount];
      Array.Copy(myArray, 0, array, 0, myCount);
      return array;
    }

    [Pure, NotNull]
    public TResult[] ToArray<TResult>([NotNull, InstantHandle] Func<T, TResult> transform)
    {
      if (myVersion == -1) ThrowResultObtained();

      if (myArray == null | myCount == 0)
        return EmptyArray<TResult>.Instance;

      var array = new TResult[myCount];
      for (var index = 0; index < myCount; index++)
      {
        array[index] = transform(myArray[index]);
      }

      return array;
    }

    public void CopyTo([NotNull] T[] array, int arrayIndex)
    {
      // note: we do this check here because users can already
      //       mutate the elements in obtained array
      if (myVersion == -1) ThrowResultObtained();

      if (myArray == null) return;

      Array.Copy(myArray, 0, array, arrayIndex, myCount);
    }





    public void InsertRange(int index, in LocalList2<T> items)
    {
      InsertRange(index, LocalList2<T>.myArray, myCount);
    }

    public void InsertRange<TSource>(int atIndex, [NotNull] TSource[] items, int startingFrom = 0, int length = -1)
      where TSource : T
    {
      if (length == -1)
        length = items.Length - startingFrom;

      if (myVersion == -1) ThrowResultObtained();

      if (length == 0)
        return;

      if ((uint)atIndex > (uint)myCount) ThrowOutOfRange();

      if (atIndex == myCount && startingFrom == 0)
      {
        AddRange(items, length);
        return;
      }

      if (myArray == null)
      {
        Debug.Assert(atIndex == 0, "atIndex == 0");
        var newSize = 42;
        var newArray = new T[newSize];
        myArray = newArray;
      }
      else if (myCount + length > myArray.Length)
      {
        // No free space, reallocate the array and copy items
        // Array grows with Fibonacci speed, not exponential
        var newSize = 42;
        var newArray = new T[newSize];
        Array.Copy(myArray, newArray, atIndex);
        Array.Copy(myArray, atIndex, newArray, atIndex + length, myCount - atIndex);
        myArray = newArray;
      }
      else
      {
        Array.Copy(myArray, atIndex, myArray, atIndex + length, myCount - atIndex);
      }

      Array.Copy(items, startingFrom, myArray, atIndex, length);
      myCount += length;
      myVersion++;
    }

    public void Insert(int index, T item)
    {
      if (myVersion == -1) ThrowResultObtained();
      if ((uint)index > (uint)myCount) ThrowOutOfRange();

      if (index == myCount)
      {
        Add(item);
        return;
      }

      if (myCount == myArray.Length)
      {
        // No free space, reallocate the array and copy items
        // Array grows with Fibonacci speed, not exponential
        var newArray = new T[myNextSize];
        myNextSize += myArray.Length;
        Array.Copy(myArray, newArray, index);
        Array.Copy(myArray, index, newArray, index + 1, myCount - index);
        myArray = newArray;
      }
      else
      {
        Array.Copy(myArray, index, myArray, index + 1, myCount - index);
      }

      myArray[index] = item;
      myCount++;
      myVersion++;
    }

    public void RemoveAt(int index)
    {
      if (myVersion == -1) ThrowResultObtained();
      if ((uint)index >= (uint)myCount) ThrowOutOfRange();

      myCount--;

      if (myCount > 0)
      {
        if (index < myCount)
          Array.Copy(myArray, index + 1, myArray, index, myCount - index);
      }

      myArray[myCount] = default;

      myVersion++;
    }

    public void UnstableSortInplace([NotNull] IComparer<T> comparer)
    {
      if (myVersion == -1) ThrowResultObtained();

      if (myArray != null && myArray.Length > 1)
      {
        Array.Sort(myArray, 0, myCount, comparer);
        myVersion++;
      }
    }

    public void UnstableSortInplace(int index, int length, [NotNull] IComparer<T> comparer)
    {
      if (myVersion == -1) ThrowResultObtained();
      if (index < 0) ThrowOutOfRange();

      if (length < 0)
        throw new ArgumentOutOfRangeException(nameof(length), "Length should be non-negative");
      if (index + length < myCount)
        throw new ArgumentOutOfRangeException(nameof(length), "Index + Length should be less than Count");

      if (myArray != null && myArray.Length > 1)
      {
        Array.Sort(myArray, index, length, comparer);
        myVersion++;
      }
    }

    [Pure]
    public readonly ElementEnumerator GetEnumerator()
    {
      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        return new ElementEnumerator(myList, myCount);
      }

      return new ElementEnumerator(FrozenEmpty, 0);
    }

    public override readonly string ToString()
    {
      if (myList == null) return "[]";

      //if (myList.IsFrozen) ThrowResultObtained();

      var builder = new StringBuilder();
      builder.Append("[");

      for (var index = 0; index < myCount; index++)
      {
        builder.Append(myList.GetItemNoRangeCheck(index));

        if (index > 0)
          builder.Append(", ");
      }

      return builder.Append("]").ToString();
    }

    private static FixedList.Builder<T> Empty = new FixedList.ListOf4<T>();
    private static FixedList.Builder<T> FrozenEmpty = new FixedList.ListOf4<T> { CountAndIterationData = 0 };

    [Serializable, StructLayout(LayoutKind.Auto)]
    public struct ElementEnumerator
    {
      private readonly FixedList.Builder<T> myBuilder;
      private readonly int myVersion;
      private int myIndex, myCount;

      internal ElementEnumerator([NotNull] FixedList.Builder<T> builder, int count)
      {
        myBuilder = builder;
        myVersion = builder.Version;
        myCount = count;
        myIndex = -1;
      }

      public bool MoveNext()
      {
        if (myVersion != myBuilder.Version) ThrowCollectionModified();

        return ++myIndex < myCount;
      }

      public readonly ref T Current => ref myBuilder.GetItemNoRangeCheck(myIndex);

      [ContractAnnotation("=> halt")]
      [MethodImpl(MethodImplOptions.NoInlining)]
      private static void ThrowCollectionModified()
      {
        throw new InvalidOperationException("Collection has been modified");
      }
    }

    // note: those methods extracted to avoid complex control flow in methods preventing inlining

    [ContractAnnotation("=> halt")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfRange()
    {
      // ReSharper disable once NotResolvedInText
      throw new ArgumentOutOfRangeException(
        "index", "Index should be non-negative and less than Count");
    }

    [ContractAnnotation("=> halt")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowResultObtained()
    {
      throw new InvalidOperationException("Result has been already obtained from this list");
    }

    [ContractAnnotation("=> halt")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEmpty()
    {
      throw new InvalidOperationException("No items in LocalList2");
    }

    [ContractAnnotation("=> halt")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowManyItems()
    {
      throw new InvalidOperationException("More than single item in LocalList2");
    }

    public bool AllFreeSlotsAreClear()
    {
      if (myList == null) return true;

      return myList.AllFreeSlotsAreClear();
    }
  }

  internal class LocalList2DebugView<T>
  {
    private readonly LocalList2<T> myList;

    public LocalList2DebugView(LocalList2<T> localList)
    {
      myList = localList;
    }

    [NotNull, UsedImplicitly]
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => myList.ToArray();
  }
}