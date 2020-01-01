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

      Debug.Assert(capacity >= 0, "capacity >= 0");

      myCount = 0;

      if (forceUseArray && capacity > 0)
        myList = new FixedList.ListOfArray<T>(capacity);
      else
        myList = CreateBuilderWithCapacity(capacity);
    }

    // todo: ctors

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

    public void EnsureCapacity(int capacity)
    {
      if (myList == null)
      {
        myList = CreateBuilderWithCapacity(capacity);
      }
      else
      {
        if (myList.IsFrozen) ThrowResultObtained();

        if (capacity <= myList.Capacity) return;

        var newList = CreateBuilderWithCapacity(capacity);
        Debug.Assert(newList != null);

        for (var index = 0; index < myCount; index++)
        {
          newList.ItemRefNoRangeCheck(index) = myList.ItemRefNoRangeCheck(index);
        }

        newList.CountAndIterationData = myList.CountAndIterationData;
        myList = newList;
      }
    }

    [Pure, CanBeNull]
    private static FixedList.Builder<T> CreateBuilderWithCapacity(int capacity)
    {
      switch (capacity)
      {
        case 0: return null;
        case 1: return new FixedList.ListOf1<T>();
        case 2: return new FixedList.ListOf2<T>();
        case 3: return new FixedList.ListOf3<T>();
        case 4: return new FixedList.ListOf4<T>();
        case 5:
        case 6:
        case 7:
        case 8: return new FixedList.ListOf8<T>();
        default: return new FixedList.ListOfArray<T>(capacity);
      }
    }

    public void TrimExcess()
    {
      if (myList == null) return;
      if (myList.IsFrozen) ThrowResultObtained();

      myList = myList.TrimExcess(myCount, false);
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
        builder.Append(myList.ItemRefNoRangeCheck(index));

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

      for (int index = myCount, capacity = myList.Capacity; index < capacity; index++)
      {
        var item = myList.ItemRefNoRangeCheck(index);
        if (!EqualityComparer<T>.Default.Equals(item, default))
          return false;
      }

      return true;
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