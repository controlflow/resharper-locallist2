using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Util.DataStructures.Collections;
// ReSharper disable MergeConditionalExpression
// ReSharper disable once CheckNamespace

namespace JetBrains.Util
{
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
    [CanBeNull] private FixedList.Builder<T> myList;
    private int myCount;

    private static FixedList.Builder<T> FrozenEmpty = new FixedList.ListOf4<T> { CountAndIterationData = 0 };

    #region Constructors

    public LocalList2(in LocalList2<T> other, bool preserveCapacity = false)
    {
      myCount = other.myCount;

      var otherList = other.myList;
      if (otherList != null)
      {
        if (otherList.IsFrozen) ThrowResultObtained();

        myList = preserveCapacity
          ? otherList.Clone(myCount)
          : otherList.TrimExcess(myCount, clone: true);
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

      Assertion.Assert(capacity >= 0, "capacity >= 0");

      myCount = 0;

      if (forceUseArray && capacity > 0)
        myList = new FixedList.ListOfArray<T>(capacity);
      else
        myList = CreateBuilderWithCapacity(capacity);
    }

    public LocalList2([NotNull] IEnumerable<T> enumerable)
    {
      if (enumerable == null)
        throw new ArgumentNullException(nameof(enumerable));

      myList = null;
      myCount = 0;
      InsertRange(index: 0, enumerable);
    }

    #endregion
    #region Manipulations

    /// <summary>
    /// Gets the number of elements contained in the <see cref="LocalList2{T}"/>.
    /// </summary>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public readonly int Count => myCount;

    public readonly T this[int index]
    {
      get
      {
        if (myList == null) ThrowOutOfRange();
        if (myList.IsFrozen) ThrowResultObtained(); // note: can be relaxed

        if ((uint) index >= (uint) myCount) ThrowOutOfRange();

        return myList.ItemRefNoRangeCheck(index);
      }
      set
      {
        if (myList == null) ThrowOutOfRange();
        if (myList.IsFrozen) ThrowResultObtained();

        if ((uint) index >= (uint) myCount) ThrowOutOfRange();

        myList.ItemRefNoRangeCheck(index) = value;
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

    public bool Remove(T item)
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      var index = myList.IndexOf(item, myCount);
      if (index < 0) return false;

      myList.ModifyVersion();
      myList.RemoveAt(index, myCount);
      myCount--;
      return true;
    }

    public void RemoveAt(int index)
    {
      if (myList == null) ThrowOutOfRange();
      if (myList.IsFrozen) ThrowResultObtained();

      if ((uint) index >= (uint) myCount) ThrowOutOfRange();

      myList.ModifyVersion();
      myList.RemoveAt(index, myCount);
      myCount--;
    }

    public void Insert(int index, T item)
    {
      if ((uint) index >= (uint) myCount) ThrowOutOfRange();

      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion(); // modify before EnsureCapacity()
      }

      EnsureCapacity(capacity: myCount + 1);

      Assertion.Assert(myList != null);

      // shift tail items if needed
      myList.CopyToImpl(
        target: myList, targetIndex: index + 1,
        fromIndex: index, length: myCount - index);

      myList.ItemRefNoRangeCheck(index) = item;
      myCount++;
    }

    public void Clear()
    {
      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion();
        myList.Clear(myCount);
        myCount = 0;
      }
    }

    public void Reverse()
    {
      Reverse(startIndex: 0, length: myCount);
    }

    public void Reverse(int startIndex, int length)
    {
      if (startIndex < 0)
        throw new ArgumentOutOfRangeException(
          nameof(startIndex), "Index should be non-negative and less then Count");
      if (length < 0)
        throw new ArgumentOutOfRangeException(nameof(length), "Length should be non-negative");
      if (startIndex + length > myCount)
        throw new ArgumentException("Index plus length is beyond list's items Count");

      if (myList == null) return;
      if (myList.IsFrozen) ThrowResultObtained();

      myList.ModifyVersion();

      if (myCount > 1)
      {
        myList.Reverse(startIndex: 0, length: myCount);
      }
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

    #endregion
    #region Results gathering

    public readonly void CopyTo([NotNull] T[] array, int arrayIndex)
    {
      if (array == null)
        throw new ArgumentNullException(nameof(array));
      if (arrayIndex < 0)
        throw new ArgumentOutOfRangeException(nameof(arrayIndex));
      if (arrayIndex + myCount > array.Length)
        throw new ArgumentException(
          $"The number of items in {nameof(LocalList2<int>)} is greater than the available array space");

      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.CopyToImpl(array, arrayIndex, myCount);
      }
    }

    [MustUseReturnValue, NotNull]
    public T[] ToArray()
    {
      if (myList == null)
      {
        myList = FrozenEmpty;
        return EmptyArray<T>.Instance;
      }

      if (myList.IsFrozen) ThrowResultObtained();

      myList.Freeze(myCount);

      // if internal array is the same as `myCount`
      // just return the internal array, but beware it can be mutated after
      var internalArray = myList.TryGetInternalArray();
      if (internalArray != null && internalArray.Length == myCount)
      {
        return internalArray;
      }

      var newArray = new T[myCount];
      myList.CopyToImpl(newArray, 0, myCount);

      return newArray;
    }

    [MustUseReturnValue, NotNull]
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

    [MustUseReturnValue, NotNull]
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

    #endregion
    #region Range manipulations

    public void AddRange([NotNull] IEnumerable<T> items)
    {
      InsertRange(index: myCount, items);
    }

    public void AddRange([NotNull] ICollection<T> collection)
    {
      InsertRange(index: myCount, collection);
    }

    public void AddRange(in LocalList2<T> list)
    {
      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion(); // modify before EnsureCapacity()
      }

      var otherCount = list.myCount;
      if (otherCount == 0) return; // do nothing

      EnsureCapacity(myCount + otherCount);

      Assertion.Assert(myList != null);

      myList.ModifyVersion();

      var otherList = list.myList;
      if (otherList != null)
      {
        otherList.CopyToImpl(myList, targetIndex: myCount, fromIndex: 0, otherCount);
        myCount += otherCount;
      }
    }

    public void InsertRange(int index, [NotNull] IEnumerable<T> items)
    {
      if (index < 0 || index > myCount)
        throw new ArgumentOutOfRangeException(
          nameof(index), "Index should be non-negative and less then or equal to Count");
      if (items == null)
        throw new ArgumentNullException(nameof(items));

      if (items is ICollection<T> collection)
      {
        InsertRange(index, collection);
        return;
      }

      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion();
      }

      // if index is != 0, just append items to the tail first
      var newItemsStartIndex = myCount;

      foreach (var item in items)
      {
        if (myList == null)
          myList = new FixedList.ListOf4<T>();

        myList.Append(in item, myCount++, ref myList);
      }

      if (index < newItemsStartIndex) // and later rotate them
      {
        Assertion.Assert(myList != null);

        // cool trick to rotate in-place in O(2n)
        myList.Reverse(index, length: newItemsStartIndex - index);
        myList.Reverse(newItemsStartIndex, length: myCount - newItemsStartIndex);
        myList.Reverse(index, length: myCount - index);
      }
    }

    public void InsertRange(int index, [NotNull] ICollection<T> collection)
    {
      if (collection == null)
        throw new ArgumentNullException(nameof(collection));

      if (myList != null)
      {
        if (myList.IsFrozen) ThrowResultObtained();

        myList.ModifyVersion(); // modify before EnsureCapacity()
      }

      var collectionCount = collection.Count;
      if (collectionCount == 0) return; // do nothing

      EnsureCapacity(myCount + collectionCount);

      Assertion.Assert(myList != null);

      // shift tail items if needed
      myList.CopyToImpl(
        target: myList, targetIndex: index + collectionCount,
        fromIndex: index, length: myCount - index);

      if (myList.TryGetInternalArray() is { } internalArray)
      {
        // directly copy to internal array
        collection.CopyTo(internalArray, arrayIndex: index);
      }
      else if (collection is IList<T> list)
      {
        // avoid using IEnumerable<T> if possible, since it's likely
        // IEnumerator allocation + many interface calls
        for (var listIndex = 0; listIndex < collectionCount; listIndex++)
        {
          myList.ItemRefNoRangeCheck(index + listIndex) = list[listIndex];
        }
      }
      else
      {
        var targetIndex = index;

        foreach (var item in collection)
        {
          myList.ItemRefNoRangeCheck(targetIndex++) = item;
        }
      }

      myCount += collectionCount;
    }

    #endregion
    #region LINQ-like methods

    [Pure]
    public readonly bool Any()
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      return myCount > 0;
    }

    [Pure]
    public readonly bool Any([InstantHandle, NotNull] Func<T, bool> predicate)
    {
      if (myList == null) return false;
      if (myList.IsFrozen) ThrowResultObtained();

      for (var index = 0; index < myCount; index++)
      {
        if (predicate(myList.ItemRefNoRangeCheck(index)))
          return true;
      }

      return false;
    }

    [Pure]
    public readonly bool All([InstantHandle, NotNull] Func<T, bool> predicate)
    {
      if (myList == null) return true;
      if (myList.IsFrozen) ThrowResultObtained();

      for (var index = 0; index < myCount; index++)
      {
        if (!predicate(myList.ItemRefNoRangeCheck(index)))
          return false;
      }

      return true;
    }

    [Pure]
    public readonly T First()
    {
      if (myList == null) ThrowEmpty();
      if (myList.IsFrozen) ThrowResultObtained();
      if (myCount == 0) ThrowEmpty();

      return myList.ItemRefNoRangeCheck(0);
    }

    [Pure]
    public readonly T Last()
    {
      if (myList == null) ThrowEmpty();
      if (myList.IsFrozen) ThrowResultObtained();
      if (myCount == 0) ThrowEmpty();

      return myList.ItemRefNoRangeCheck(myCount - 1);
    }

    [Pure]
    public readonly T Single()
    {
      if (myList == null) ThrowEmpty();
      if (myList.IsFrozen) ThrowResultObtained();

      if (myCount == 0) ThrowEmpty();
      if (myCount > 1) ThrowManyItems();

      return myList.ItemRefNoRangeCheck(0);
    }

    [CanBeNull]
    public readonly T SingleItem
    {
      get
      {
        if (myList == null) return default;
        if (myList.IsFrozen) ThrowResultObtained();

        if (myCount > 1) return default;

        return myList.ItemRefNoRangeCheck(0);
      }
    }

    [Pure, CanBeNull]
    public readonly T FirstOrDefault()
    {
      if (myList == null) return default;
      if (myList.IsFrozen) ThrowResultObtained();

      // note: intentionally no `myCount` checks
      return myList.ItemRefNoRangeCheck(0);
    }

    [Pure]
    public readonly T SingleOrDefault()
    {
      if (myList == null) return default;
      if (myList.IsFrozen) ThrowResultObtained();

      if (myCount == 0) return default;
      if (myCount > 1) ThrowManyItems();

      // note: intentionally no `myCount` checks
      return myList.ItemRefNoRangeCheck(0);
    }

    [Pure, CanBeNull]
    public readonly T LastOrDefault()
    {
      if (myList == null) return default;
      if (myList.IsFrozen) ThrowResultObtained();

      if (myCount == 0) return default;

      return myList.ItemRefNoRangeCheck(myCount - 1);
    }

    #endregion
    #region Capacity management

    public int Capacity
    {
      readonly get => myList == null ? 0 : myList.Capacity;
      set
      {
        if (value < myCount) ThrowOutOfRange();

        if (myList != null && myList.IsFrozen) ThrowResultObtained();

        if (value == Capacity) return;

        // re-allocate storage
        var newList = CreateBuilderWithCapacity(value);

        if (myList != null && newList != null)
        {
          myList.CopyToImpl(newList, targetIndex: 0, fromIndex: 0, length: myCount);
        }

        myList = newList;
      }
    }

    public void EnsureCapacity(int capacity)
    {
      if (myList == null)
      {
        myList = CreateBuilderWithCapacity(Normalize(capacity));
        return;
      }

      if (myList.IsFrozen) ThrowResultObtained();

      var currentCapacity = myList.Capacity;
      if (currentCapacity >= capacity) return;

      var newCapacity = Math.Max(currentCapacity * 2, capacity);

      var newList = CreateBuilderWithCapacity(Normalize(newCapacity));
      Assertion.Assert(newList != null);

      myList.CopyToImpl(newList, targetIndex: 0, fromIndex: 0, myCount);
      myList = newList;

      static int Normalize(int cap) => cap <= 0 ? 0 : cap <= 4 ? 4 : cap <= 8 ? 8 : cap;
    }

    [Pure, CanBeNull]
    private static FixedList.Builder<T> CreateBuilderWithCapacity(int capacity)
    {
      return capacity switch
      {
        0 => null,
        1 => new FixedList.ListOf1<T>(),
        2 => new FixedList.ListOf2<T>(),
        3 => new FixedList.ListOf3<T>(),
        4 => new FixedList.ListOf4<T>(),
        8 => new FixedList.ListOf8<T>(),
        _ => new FixedList.ListOfArray<T>(capacity)
      };
    }

    public void TrimExcess()
    {
      if (myList == null) return;
      if (myList.IsFrozen) ThrowResultObtained();

      var trimmedList = myList.TrimExcess(myCount, clone: false);
      if (trimmedList != null)
      {
        trimmedList.CountAndIterationData = myList.CountAndIterationData;
      }

      myList = trimmedList;
    }

    #endregion
    #region Sorting

    public void UnstableSort()
    {
      UnstableSort(0, myCount, Comparer<T>.Default);
    }

    public void UnstableSort([NotNull] Comparison<T> comparison)
    {
      if (comparison == null)
        throw new ArgumentNullException(nameof(comparison));

      UnstableSort(0, myCount, new DelegateComparer(comparison));
    }

    private sealed class DelegateComparer : IComparer<T>
    {
      [NotNull] private readonly Comparison<T> myComparison;

      public DelegateComparer([NotNull] Comparison<T> comparison)
      {
        myComparison = comparison;
      }

      public int Compare(T x, T y) => myComparison(x, y);
    }

    public void UnstableSort([NotNull] IComparer<T> comparer)
    {
      UnstableSort(0, myCount, comparer);
    }

    public void UnstableSort(int index, int length, [NotNull] IComparer<T> comparer)
    {
      if (index < 0)
        ThrowOutOfRange();
      if (comparer == null)
        throw new ArgumentNullException(nameof(comparer));
      if (length < 0)
        throw new ArgumentOutOfRangeException(nameof(length));
      if (index + length > myCount)
        throw new ArgumentException(
          $"The number of items in {nameof(LocalList2<int>)} is greater than the available array space");

      if (myList == null) return;

      if (myList.IsFrozen) ThrowResultObtained();

      myList.ModifyVersion();

      if (myCount > 1)
      {
        myList.Sort(0, myCount, comparer);
      }
    }

    #endregion
    #region Enumeration

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

    [Serializable, StructLayout(LayoutKind.Auto)]
    public struct ElementEnumerator
    {
      [NotNull] private readonly FixedList.Builder<T> myBuilder;
      private readonly int myVersion;
      private int myIndex, myCount;

      internal ElementEnumerator([NotNull] FixedList.Builder<T> builder, int count)
      {
        myBuilder = builder;
        myVersion = builder.CountAndIterationData;
        myCount = count;
        myIndex = -1;
      }

      public bool MoveNext()
      {
        if (myVersion != myBuilder.CountAndIterationData) ThrowCollectionModified();

        return ++myIndex < myCount;
      }

      public readonly ref T Current => ref myBuilder.ItemRefNoRangeCheck(myIndex);

      [ContractAnnotation("=> halt")]
      [MethodImpl(MethodImplOptions.NoInlining)]
      private static void ThrowCollectionModified()
      {
        throw new InvalidOperationException("Collection has been modified");
      }
    }

    #endregion
    #region Throw helpers

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

    #endregion
    #region Debug & presentation

    [Pure, NotNull]
    public override readonly string ToString()
    {
      if (myList == null) return "[]";

      if (myList.IsFrozen) ThrowResultObtained();

      var builder = new StringBuilder("[");

      for (var index = 0; index < myCount; index++)
      {
        if (index > 0) builder.Append(", ");

        var itemText = myList.ItemRefNoRangeCheck(index).ToString();
        builder.Append(itemText);
      }

      return builder.Append("]").ToString();
    }

    [MustUseReturnValue, NotNull]
    internal readonly T[] ToDebugArray()
    {
      if (myList == null)
      {
        return EmptyArray<T>.Instance;
      }

      var internalArray = myList.TryGetInternalArray();
      if (internalArray != null && internalArray.Length == myCount)
      {
        return internalArray;
      }

      var newArray = new T[myCount];
      myList.CopyToImpl(newArray, 0, myCount);
      return newArray;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly bool AllFreeSlotsAreClear()
    {
      if (myList == null) return true;

      for (int index = myCount, capacity = myList.Capacity; index < capacity; index++)
      {
        var item = myList.ItemRefNoRangeCheck(index);
        if (!EqualityComparer<T>.Default.Equals(item, default))
          return false;
      }

      return true;
    }

    #endregion
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
    public T[] Items => myList.ToDebugArray();
  }
}