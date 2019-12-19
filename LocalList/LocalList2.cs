using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Util.DataStructures.Collections;

// ReSharper disable once CheckNamespace
namespace JetBrains.Util
{
  // NOTE: [downside] may allocate array instead of returning internal one

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

    // todo: to be removed
    [Obsolete] private static T[] myArray;
    [Obsolete] private static int myCount;
    [Obsolete] private static int myNextSize;
    [Obsolete] private static int myVersion;

    public LocalList2(in LocalList2<T> other)
    {
      myList = other.myList?.Clone();
    }

    public LocalList2(int capacity, bool forceUseArray = false)
    {
      if (capacity < 0)
        throw new ArgumentOutOfRangeException(nameof(capacity));

      Debug.Assert(capacity >= 0, "capacity >= 0");

      if (forceUseArray && capacity > 0)
      {
        myList = new FixedList.ListOfArray<T>(new T[capacity], count: 0);
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
          default: myList = new FixedList.ListOfArray<T>(new T[capacity], capacity); break;
        }
      }
    }

    public LocalList2([NotNull] IEnumerable<T> enumerable)
    {
      myList = null;
      AddRange(enumerable);
    }

    public LocalList2([NotNull] T[] array, bool copyArray = true)
    {
      if (copyArray)
      {
        myList = null;
        AddRange(array);
      }
      else
      {
        myList = new FixedList.ListOfArray<T>(array, array.Length);
      }
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="LocalList2{T}"/>.
    /// </summary>
    public int Count
    {
      get
      {
        if (myList == null) return 0;

        return myList.Count; // virtual call
      }
    }

    // ReSharper disable once MergeConditionalExpression
    public int Capacity
    {
      get
      {
        if (myList == null) return 0;

        return myList.Capacity;
      }
    }

    public T this[int index]
    {
      get
      {
        return myList[index];

        if (myVersion == -1) ThrowResultObtained();
        if ((uint)index >= (uint)myCount) ThrowOutOfRange();

        return myArray[index];
      }
      set
      {
        if (myVersion == -1) ThrowResultObtained();
        if ((uint)index >= (uint)myCount) ThrowOutOfRange();

        myArray[index] = value;
      }
    }

    [Pure] public bool Any() => myCount > 0;

    [Pure]
    public bool Any([InstantHandle, NotNull] Func<T, bool> predicate)
    {
      for (var index = 0; index < myCount; index++)
      {
        if (predicate(myArray[index]))
          return true;
      }

      return false;
    }

    [Pure]
    public T Last()
    {
      if (myCount == 0) ThrowEmpty();

      return myArray[myCount - 1];
    }

    [Pure]
    public T First()
    {
      if (myCount == 0) ThrowEmpty();

      return myArray[0];
    }

    [Pure]
    public T Single()
    {
      if (myCount == 0) ThrowEmpty();
      if (myCount > 1) ThrowManyItems();

      return myArray[0];
    }

    [CanBeNull] public T SingleItem => myCount == 1 ? myArray[0] : default;

    [Pure, CanBeNull]
    public T FirstOrDefault()
    {
      if (myCount == 0)
        return default;

      return myArray[0];
    }

    [Pure, CanBeNull]
    public T LastOrDefault()
    {
      if (myCount == 0)
        return default;

      return myArray[myCount - 1];
    }

    public void Add(T item)
    {
      if (myVersion == -1) ThrowResultObtained();

      // If there were no array, just create new
      if (myArray == null)
      {
        myArray = new T[DefaultFirstSize];
        myNextSize = DefaultFirstSize * 2;
      }
      else if (myCount == myArray.Length)
      {
        var newArray = new T[myNextSize];
        myNextSize += myArray.Length;

        Array.Copy(myArray, 0, newArray, 0, myCount);
        myArray = newArray;
      }

      myArray[myCount++] = item;
      myVersion++;
    }

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

    // Changes myNextSize as side-effect
    private int CalculateNewAndNextSize(int currentSize, int capacity)
    {
      int newSize;
      if (currentSize == 0)
      {
        newSize = DefaultFirstSize;
        myNextSize = DefaultFirstSize * 2;
      }
      else
      {
        newSize = myNextSize;
        myNextSize += currentSize;
      }

      while (newSize < capacity)
      {
        var size = newSize;
        newSize = myNextSize;
        myNextSize += size;
      }

      return newSize;
    }

    public void EnsureCapacity(int capacity, bool exact = false)
    {
      var currentSize = Capacity;
      if (capacity <= currentSize) return;

      var newSize = CalculateNewAndNextSize(currentSize, capacity);
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

    public bool Remove(T item)
    {
      if (myVersion == -1) ThrowResultObtained();

      if (myArray == null)
        return false;

      var index = IndexOf(item);
      if (index < 0)
        return false;

      RemoveAt(index);
      return true;
    }

    [Pure]
    public bool Contains(T item)
    {
      if (myVersion == -1) ThrowResultObtained();

      var index = IndexOf(item);
      return index >= 0;
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

    public void Clear()
    {
      if (myVersion == -1) ThrowResultObtained();

      if (myCount > 0)
      {
        Array.Clear(myArray, 0, myCount);
        myCount = 0;
      }

      myVersion++;
    }

    [Pure]
    public int IndexOf(T item)
    {
      if (myVersion == -1) ThrowResultObtained();

      if (myArray == null)
        return -1;

      return Array.IndexOf(myArray, item, 0, myCount);
    }

    public void InsertRange(int index, in LocalList2<T> items)
    {
      InsertRange(index, LocalList2<T>.myArray, LocalList2<T>.myCount);
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
        var newSize = CalculateNewAndNextSize(0, myCount + length);
        var newArray = new T[newSize];
        myArray = newArray;
      }
      else if (myCount + length > myArray.Length)
      {
        // No free space, reallocate the array and copy items
        // Array grows with Fibonacci speed, not exponential
        var newSize = CalculateNewAndNextSize(myArray.Length, myCount + length);
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
    public ElementEnumerator GetEnumerator()
    {
      if (myVersion == -1) ThrowResultObtained();

      return new ElementEnumerator(this);
    }

    [Pure, NotNull]
    public IList<T> ResultingList()
    {
      return (IList<T>)ReadOnlyList();
    }

    [Pure, NotNull]
    public IReadOnlyList<T> ReadOnlyList()
    {
      if (myVersion == -1) ThrowResultObtained();

      myVersion = -1;

      return FixedList.FromArray(myArray, myCount);
    }

    public override string ToString()
    {
      if (myArray == null) return "[]";

      var builder = new StringBuilder();
      builder.Append("[");
      for (var index = 0; index < myArray.Length; index++)
      {
        builder.Append(myArray[index]);

        if (index < myCount - 1)
          builder.Append(", ");
      }

      builder.Append("]");
      return builder.ToString();
    }

    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ElementEnumerator
    {
      private readonly LocalList2<T> myList;
      private readonly int myVersion;
      private int myIndex;
      private T myCurrent;

      internal ElementEnumerator(in LocalList2<T> collection)
      {
        myList = collection;
        myVersion = LocalList2<T>.myVersion;
        myIndex = 0;
        myCurrent = default;
      }

      public bool MoveNext()
      {
        if (myVersion != LocalList2<T>.myVersion)
          ThrowCollectionModified();

        if (myIndex < LocalList2<T>.myCount)
        {
          myCurrent = LocalList2<T>.myArray[myIndex];
          myIndex++;
          return true;
        }

        myIndex = myCount + 1;
        myCurrent = default;
        return false;
      }

      public T Current => myCurrent;

      private static void ThrowCollectionModified()
      {
        throw new InvalidOperationException("Collection has been modified");
      }
    }

    // note: those methods extracted to avoid complex control flow in methods preventing inlining

    private static void ThrowOutOfRange()
    {
      // ReSharper disable once NotResolvedInText
      throw new ArgumentOutOfRangeException("index", "Index should be non-negative and less than Count");
    }

    private static void ThrowResultObtained()
    {
      throw new InvalidOperationException("Result has been already obtained from this list");
    }

    private static void ThrowEmpty()
    {
      throw new InvalidOperationException("No items in LocalList2");
    }

    private static void ThrowManyItems()
    {
      throw new InvalidOperationException("More than single item in LocalList2");
    }
  }

  internal class LocalList2DebugView<T>
  {
    private readonly LocalList2<T> myList;

    public LocalList2DebugView(LocalList2<T> localList)
    {
      myList = localList;
    }

    [NotNull, DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => myList.ToArray();
  }
}