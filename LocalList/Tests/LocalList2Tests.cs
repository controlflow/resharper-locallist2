using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable AssignmentIsFullyDiscarded
// ReSharper disable UseIndexFromEndExpression

namespace JetBrains.Util.Tests
{
  [TestFixture]
  public sealed class LocalList2Tests
  {
    // for debugging:
    //Console.WriteLine($"count={list.Count}, capacity={list.Capacity}");
    //if (list.Count != 0 || list.Capacity != 1) continue;

    [Test]
    public void DefaultConstructor()
    {
      var list = new LocalList2<int>();

      Assert.AreEqual(0, list.Count);
      Assert.AreEqual(0, list.Capacity);
    }

    [Test]
    public void CapacityConstructor()
    {
      Assert.Throws<ArgumentOutOfRangeException>(() =>
      {
        var unused = new LocalList2<int>(capacity: -1);
      });

      foreach (var forceArray in new[] { true, false })
      {
        for (var capacity = 0; capacity < 30; capacity++)
        {
          var list = new LocalList2<int>(capacity: capacity, forceArray);

          Assert.AreEqual(0, list.Count);
          Assert.AreEqual(capacity, list.Capacity);
        }
      }
    }

    [Test]
    public void CloneConstructor()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var sameCapacityClone = new LocalList2<int>(in list, preserveCapacity: true);

        Assert.AreEqual(list.Count, sameCapacityClone.Count);
        Assert.AreEqual(list.Capacity, sameCapacityClone.Capacity);
        Assert.IsTrue(sameCapacityClone.AllFreeSlotsAreClear());

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(list[index], sameCapacityClone[index]);
        }

        sameCapacityClone.Add(42);
        Assert.AreEqual(list.Count + 1, sameCapacityClone.Count);
        Assert.GreaterOrEqual(sameCapacityClone.Capacity, list.Capacity);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        var possibleLessCapacityClone = new LocalList2<int>(in list, preserveCapacity: false);

        Assert.AreEqual(list.Count, possibleLessCapacityClone.Count);
        Assert.LessOrEqual(possibleLessCapacityClone.Capacity, list.Capacity);
        Assert.IsTrue(possibleLessCapacityClone.AllFreeSlotsAreClear());

        possibleLessCapacityClone.Add(42);
        Assert.AreEqual(list.Count + 1, possibleLessCapacityClone.Count);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        var unused1 = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() =>
        {
          var unused2 = new LocalList2<int>(in list, preserveCapacity: true);
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
          var unused3 = new LocalList2<int>(in list, preserveCapacity: false);
        });
      }
    }

    [Test]
    public void AddCountIndexer()
    {
      foreach (var count in CapacitiesToTest.Concat(new[] { 70000 }))
      {
        var list = new LocalList2<byte>();
        Assert.AreEqual(0, list.Count);

        var bytes = NonZeroBytes(count).ToArray();

        foreach (var x in bytes)
        {
          list.Add(x);
        }

        Assert.AreEqual(count, list.Count);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(bytes[index], list[index]);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[-1] = 42);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[list.Count]);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[list.Count] = 42);

        // store in reverse via indexer setter
        for (var index = 0; index < bytes.Length; index++)
        {
          list[list.Count - index - 1] = bytes[index];
        }

        if (list.Count > 0)
        {
          var enumerator = list.GetEnumerator();
          list[0] = list[0];
          Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        var resultingList = list.ResultingList();
        _ = list.Count; // do not throws

        Assert.AreEqual(list.Count, resultingList.Count);
        Assert.Throws<InvalidOperationException>(() => _ = list.ResultingList());

        // the same APIs via IList<T> interface
        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(bytes[bytes.Length - index - 1], resultingList[index]);
        }

        Assert.Throws<CollectionReadOnlyException>(() => resultingList[0] = 42);
        Assert.Throws<CollectionReadOnlyException>(() => resultingList.Add(42));

        // results obtained checks
        if (list.Count > 0)
        {
          Assert.Throws<InvalidOperationException>(() => _ = list[0]);
          Assert.Throws<InvalidOperationException>(() => list[0] = 42);
        }

        Assert.Throws<InvalidOperationException>(() => list.Add(42));
      }

      static IEnumerable<byte> NonZeroBytes(int count)
      {
        byte x = 1;
        for (var index = 0; index < count; index++)
        {
          if (x == 0) x++;

          yield return x++;
        }
      }
    }

    [Test]
    public void Enumeration01()
    {
      foreach (var capacity in CapacitiesToTest)
      {
        var list = new LocalList2<string>(capacity);
        list.Add("abc");
        list.Add("def");

        var enumerator = list.GetEnumerator();

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("abc", enumerator.Current);

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("def", enumerator.Current);

        Assert.IsFalse(enumerator.MoveNext());
      }
    }

    [Test]
    public void Enumeration02()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var enumerator1 = list.GetEnumerator();

        for (var index = 1; index <= list.Count; index++)
        {
          Assert.IsTrue(enumerator1.MoveNext());
          Assert.AreEqual(index, enumerator1.Current);
        }

        Assert.IsFalse(enumerator1.MoveNext());

        var resultingList = list.ResultingList();

        if (list.Count == 0)
          Assert.IsTrue(ReferenceEquals(resultingList, EmptyList<int>.Instance));

        Assert.Throws<InvalidOperationException>(() => _ = list.GetEnumerator());
        Assert.Throws<InvalidOperationException>(() => _ = list.ResultingList());
        Assert.Throws<InvalidOperationException>(() => _ = list.ReadOnlyList());

        var enumerator2 = resultingList.GetEnumerator(); // reused `this`
        var enumerator3 = resultingList.GetEnumerator(); // not reused one

        if (list.Count == 0)
        {
          Assert.IsTrue(ReferenceEquals(EmptyEnumerator<int>.Instance, enumerator2));
          Assert.IsTrue(ReferenceEquals(EmptyEnumerator<int>.Instance, enumerator3));
        }
        else
        {
          Assert.IsTrue(ReferenceEquals(resultingList, enumerator2));
          Assert.IsFalse(ReferenceEquals(enumerator2, enumerator3));
        }

        AssertItemsInOrder(enumerator2);
        AssertItemsInOrder(enumerator3);

        enumerator2.Dispose();

        var enumerator4 = ((IEnumerable) resultingList).GetEnumerator(); // reused
        if (list.Count != 0)
        {
          Assert.IsTrue(ReferenceEquals(enumerator2, enumerator4));
          Assert.IsTrue(ReferenceEquals(resultingList, enumerator4));
        }

        AssertItemsInOrder((IEnumerator<int>) enumerator4);

        enumerator3.Dispose();

        void AssertItemsInOrder(IEnumerator<int> enumerator)
        {
          IEnumerator oldEnumerator = enumerator;

          for (var index = 1; index <= list.Count; index++)
          {
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(index, enumerator.Current);
            Assert.AreEqual(index, oldEnumerator.Current);
          }

          Assert.IsFalse(enumerator.MoveNext());

          enumerator.Reset();

          for (var index = 1; index <= list.Count; index++)
          {
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(index, enumerator.Current);
          }

          Assert.IsFalse(enumerator.MoveNext());
        }
      }
    }

    [Test]
    public void Enumeration03()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        foreach (ref var item in list)
        {
          item = 42; // ref T Current { get; }
        }

        Assert.IsTrue(list.AllFreeSlotsAreClear());

        foreach (var item in list)
          Assert.AreEqual(item, 42);

        foreach (var item in list.ReadOnlyList())
          Assert.AreEqual(item, 42);
      }
    }

    [Test]
    public void ToArray()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var array = list.ToArray();

        Assert.AreEqual(list.Count, array.Length);

        Assert.Throws<InvalidOperationException>(() => _ = list.ToArray());
        Assert.Throws<InvalidOperationException>(() => _ = list.ResultingList());
        Assert.Throws<InvalidOperationException>(() => _ = list.ReadOnlyList());
        Assert.Throws<InvalidOperationException>(() => list.Add(42));
        Assert.Throws<InvalidOperationException>(() => _ = list[0]);

        for (var index = 0; index < array.Length; index++)
        {
          Assert.AreEqual(index + 1, array[index]);
        }
      }
    }

    [Test]
    public void Clear()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var oldCapacity = list.Capacity;
        var enumerator = list.GetEnumerator();

        list.Clear();

        Assert.AreEqual(0, list.Count);
        Assert.AreEqual(oldCapacity, list.Capacity);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        if (list.Capacity > 0)
          Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());

        list.Clear();
        Assert.AreEqual(0, list.Count);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        var iReadOnlyList = list.ReadOnlyList();
        Assert.AreEqual(0, iReadOnlyList.Count);

        Assert.Throws<InvalidOperationException>(() => list.Clear());
      }
    }

    [Test]
    public void IndexOfContains()
    {
      var random = new Random();

      foreach (var list in CreateVariousFilledLocalLists())
      {
        Assert.AreEqual(list.IndexOf(0), -1);
        Assert.IsFalse(list.Contains(0));

        if (list.Count > 0)
        {
          var valueToFind = random.Next(0, list.Count) + 1;
          var foundIndex = list.IndexOf(valueToFind);

          Assert.AreEqual(foundIndex, valueToFind - 1);
          Assert.IsTrue(list.Contains(valueToFind));
        }

        // the same over IList<T>
        var resultingList = list.ResultingList();

        Assert.AreEqual(resultingList.IndexOf(0), -1);
        Assert.IsFalse(resultingList.Contains(0));

        Assert.Throws<InvalidOperationException>(() => _ = list.IndexOf(0));
        Assert.Throws<InvalidOperationException>(() => _ = list.Contains(0));

        if (list.Count > 0)
        {
          var valueToFind = random.Next(0, list.Count) + 1;
          var foundIndex = resultingList.IndexOf(valueToFind);

          Assert.AreEqual(foundIndex, valueToFind - 1);
          Assert.IsTrue(resultingList.Contains(valueToFind));
        }
      }
    }

    [Test]
    public void Remove()
    {
      var random = new Random();

      foreach (var list in CreateVariousFilledLocalLists())
      {
        Assert.IsFalse(list.Remove(0));

        if (list.Count > 0)
        {
          var valueToRemove = random.Next(0, list.Count) + 1;
          var countBeforeRemove = list.Count;
          var enumeratorBefore = list.GetEnumerator();

          Assert.IsTrue(list.Contains(valueToRemove));
          Assert.IsTrue(list.Remove(valueToRemove));

          Assert.Throws<InvalidOperationException>(() => enumeratorBefore.MoveNext());

          var enumeratorBefore2 = list.GetEnumerator();

          Assert.IsFalse(list.Remove(valueToRemove)); // unique
          Assert.DoesNotThrow(() => enumeratorBefore2.MoveNext());

          Assert.IsTrue(list.AllFreeSlotsAreClear());

          Assert.IsFalse(list.Contains(valueToRemove));
          Assert.AreEqual(countBeforeRemove - 1, list.Count);

          var removedIndex = valueToRemove - 1;
          if (removedIndex < list.Count)
          {
            Assert.AreEqual(list[removedIndex], valueToRemove + 1);
          }
        }

        var resultingList = list.ResultingList();
        Assert.Throws<CollectionReadOnlyException>(() => resultingList.Remove(0));
        Assert.Throws<InvalidOperationException>(() => list.Remove(0));
      }
    }

    [Test]
    public void RemoveAt()
    {
      var random = new Random();

      foreach (var list in CreateVariousFilledLocalLists())
      {
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(list.Count));

        if (list.Count > 0)
        {
          var randomIndex = random.Next(0, list.Count);
          var countBeforeRemove = list.Count;
          var enumeratorBefore = list.GetEnumerator();

          list.RemoveAt(randomIndex);

          Assert.Throws<InvalidOperationException>(() => enumeratorBefore.MoveNext());

          if (randomIndex < list.Count)
          {
            Assert.AreEqual(list[randomIndex], randomIndex + 2);
          }

          Assert.AreEqual(countBeforeRemove - 1, list.Count);
          Assert.IsTrue(list.AllFreeSlotsAreClear());
        }

        var resultingList = list.ResultingList();
        Assert.Throws<InvalidOperationException>(() => list.RemoveAt(0));
        Assert.Throws<CollectionReadOnlyException>(() => resultingList.RemoveAt(0));
      }
    }

    [Test]
    public void LinqAnyAll()
    {
      var random = new Random();

      foreach (var list in CreateVariousFilledLocalLists())
      {
        Assert.AreEqual(list.Any(), list.Count > 0);

        var itemToFind = random.Next(0, list.Count) + 1;

        Assert.AreEqual(
          list.Any(x => x == itemToFind),
          list.Contains(itemToFind));

        Assert.AreEqual(
          list.All(x => x != itemToFind),
          !list.Contains(itemToFind));

        Assert.IsFalse(list.Any(x => x == 0));
        Assert.IsTrue(list.All(x => x > 0));

        _ = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() => _ = list.Any());
        Assert.Throws<InvalidOperationException>(() => _ = list.Any(x => true));
        Assert.Throws<InvalidOperationException>(() => _ = list.All(x => true));
      }
    }

    [Test]
    public void LinqFirstLastSingle()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        if (list.Count == 0)
        {
          Assert.Throws<InvalidOperationException>(() => _ = list.First());
          Assert.Throws<InvalidOperationException>(() => _ = list.Last());
          Assert.AreEqual(0, list.FirstOrDefault());
          Assert.AreEqual(0, list.LastOrDefault());
          Assert.AreEqual(0, list.SingleItem);
        }
        else
        {
          Assert.AreEqual(list[0], list.First());
          Assert.AreEqual(list[0], list.FirstOrDefault());
          Assert.AreEqual(list[list.Count - 1], list.Last());
          Assert.AreEqual(list[list.Count - 1], list.LastOrDefault());
        }

        if (list.Count == 1)
        {
          Assert.AreEqual(list[0], list.Single());
          Assert.AreEqual(list[0], list.SingleOrDefault());
          Assert.AreEqual(list[0], list.SingleItem);
        }
        else
        {
          Assert.Throws<InvalidOperationException>(() => _ = list.Single());
          Assert.AreEqual(0, list.SingleItem);

          if (list.Count > 1)
            Assert.Throws<InvalidOperationException>(() => _ = list.SingleOrDefault());
          else
            Assert.AreEqual(0, list.SingleOrDefault());
        }

        _ = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() => _ = list.First());
        Assert.Throws<InvalidOperationException>(() => _ = list.Single());
        Assert.Throws<InvalidOperationException>(() => _ = list.Last());
        Assert.Throws<InvalidOperationException>(() => _ = list.FirstOrDefault());
        Assert.Throws<InvalidOperationException>(() => _ = list.SingleOrDefault());
        Assert.Throws<InvalidOperationException>(() => _ = list.LastOrDefault());
        Assert.Throws<InvalidOperationException>(() => _ = list.SingleItem);
      }
    }

    [Test]
    public void CapacityManagement()
    {
      foreach (var list1 in CreateVariousFilledLocalLists())
      {
        var list = list1; // make mutable variable

        var oldCapacity = list.Capacity;
        var countBefore = list.Count;
        var enumeratorBefore = list.GetEnumerator();

        list.TrimExcess();
        VerifyListData();

        Assert.LessOrEqual(list.Capacity, oldCapacity);
        Assert.AreEqual(list.Count, list.Capacity);
        Assert.DoesNotThrow(() => enumeratorBefore.MoveNext());

        var enumeratorBefore2 = list.GetEnumerator();

        list.EnsureCapacity(oldCapacity);
        VerifyListData();

        if (oldCapacity == countBefore)
          Assert.AreEqual(oldCapacity, list.Capacity);
        else // EnsureCapacity() should never produce lists of capacity 1,2,3,5,6,7
          Assert.That(list.Capacity, Is.Not.InRange(1, 3).And.Not.InRange(5, 7));

        Assert.LessOrEqual(oldCapacity, list.Capacity); // resizes up to max(2x, new capacity)
        Assert.DoesNotThrow(() => enumeratorBefore2.MoveNext());

        list.EnsureCapacity(0);
        VerifyListData();

        Assert.LessOrEqual(oldCapacity, list.Capacity);

        list.EnsureCapacity(oldCapacity + 3);
        VerifyListData();

        Assert.LessOrEqual(oldCapacity + 3, list.Capacity);

        VerifyListData();

        var enumeratorBefore3 = list.GetEnumerator();
        list.Capacity = countBefore;
        Assert.AreEqual(list.Count, list.Capacity);
        Assert.DoesNotThrow(() => enumeratorBefore3.MoveNext());

        Assert.Throws<ArgumentOutOfRangeException>(() => list.Capacity = list.Count - 1);

        _ = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() => list.TrimExcess());
        Assert.Throws<InvalidOperationException>(() => list.EnsureCapacity(0));
        Assert.Throws<InvalidOperationException>(() => list.Capacity = oldCapacity);

        void VerifyListData()
        {
          Assert.AreEqual(countBefore, list.Count);

          for (var index = 0; index < list.Count; index++)
          {
            Assert.AreEqual(index + 1, list[index]);
          }

          Assert.IsTrue(list.AllFreeSlotsAreClear());
        }
      }
    }

    [Test]
    public void CopyTo()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        const int indexDelta = 42;
        var array = new int[list.Count];
        var array2 = new int[list.Count + indexDelta];

        // ReSharper disable once AssignNullToNotNullAttribute
        Assert.Throws<ArgumentNullException>(() => list.CopyTo(array: null, arrayIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.CopyTo(array, arrayIndex: -1));
        Assert.Throws<ArgumentException>(() => list.CopyTo(array, arrayIndex: 1));

        if (list.Count > 0)
          Assert.Throws<ArgumentException>(() => list.CopyTo(array, arrayIndex: list.Count));
        else
          Assert.DoesNotThrow(() => list.CopyTo(array, arrayIndex: list.Count));

        var enumeratorBefore = list.GetEnumerator();

        list.CopyTo(array, arrayIndex: 0);
        list.CopyTo(array2, arrayIndex: indexDelta);

        Assert.DoesNotThrow(() => enumeratorBefore.MoveNext());

        VerifyArrays();

        var resultingList = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() => list.CopyTo(array, arrayIndex: 0));

        resultingList.CopyTo(array, arrayIndex: 0);
        resultingList.CopyTo(array2, arrayIndex: indexDelta);

        VerifyArrays();

        // ReSharper disable once AssignNullToNotNullAttribute
        Assert.Throws<ArgumentNullException>(() => resultingList.CopyTo(array: null, arrayIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => resultingList.CopyTo(array, arrayIndex: -1));
        Assert.Catch<ArgumentException>(() => resultingList.CopyTo(array, arrayIndex: 1));

        if (list.Count > 0)
          Assert.Catch<ArgumentException>(() => resultingList.CopyTo(array, arrayIndex: list.Count));
        else
          Assert.DoesNotThrow(() => resultingList.CopyTo(array, arrayIndex: list.Count));

        void VerifyArrays()
        {
          for (var index = 0; index < array.Length; index++)
            Assert.AreEqual(index + 1, array[index]);

          for (var index = 0; index < indexDelta; index++)
            Assert.AreEqual(0, array2[index]);

          for (var index = indexDelta; index < array2.Length; index++)
            Assert.AreEqual(index + 1 - indexDelta, array2[index]);
        }
      }
    }

    [Test]
    public void AddRange()
    {
      foreach (var capacity in CapacitiesToTest.Concat(new []{ 70000 }))
      {
        var listFromEnumerable = new LocalList2<int>(enumerable: Enumerable.Range(1, capacity));
        VerifyDataAndCapacity(listFromEnumerable);

        // pure IEnumerable<T>
        var listAddRange = new LocalList2<int>(capacity: 4, forceUseArray: true);
        var enumerator = listAddRange.GetEnumerator();
        listAddRange.AddRange(items: Enumerable.Range(1, capacity));
        VerifyDataAndCapacity(listAddRange);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());

        // other LocalList
        var enumerator2 = listAddRange.GetEnumerator();
        listAddRange.AddRange(list: listFromEnumerable);
        VerifyDataAndCapacity(listAddRange, shift: capacity);
        Assert.Throws<InvalidOperationException>(() => enumerator2.MoveNext());

        var enumerator3 = listAddRange.GetEnumerator();
        listAddRange.AddRange(collection: Enumerable.Range(1, capacity).ToHashSet());
        VerifyDataAndCapacity(listAddRange, shift: capacity * 2);
        Assert.Throws<InvalidOperationException>(() => enumerator3.MoveNext());

        // IList<T> implementation
        var listAddList = new LocalList2<int>();
        listAddList.AddRange(collection: Enumerable.Range(1, capacity).ToList());
        Assert.That(listAddList.Capacity, Is.AnyOf(0, 4, 8, capacity));
        VerifyDataAndCapacity(listAddList);

        // ICollection<T> (sorted)
        var listAddSet = new LocalList2<int>();
        listAddSet.AddRange(collection: Enumerable.Range(1, capacity).ToHashSet());
        Assert.That(listAddSet.Capacity, Is.AnyOf(0, 4, 8, capacity));
        VerifyDataAndCapacity(listAddSet);

        void VerifyDataAndCapacity(in LocalList2<int> list, int shift = 0)
        {
          Assert.AreEqual(capacity + shift, list.Count);

          for (var index = 0; index < list.Count - shift; index++)
          {
            Assert.AreEqual(index + 1, list[shift + index]);
          }
        }
      }
    }

    [Test]
    public void Insert()
    {

    }

// todo: insert
    [Test]
    public void InsertRange()
    {
      var random = new Random();

      Func<IEnumerable<int>, IEnumerable<int>>[] transformations =
      {
        //ts => ts,
        ts => ts.ToList(),
        ts => ts.ToHashSet(),
      };

      foreach (var transformation in transformations)
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var insertIndex = random.Next(0, list.Count);
        var tail = Enumerable.Range(list.Count + 1, 3);

        list.InsertRange(index: insertIndex, transformation(Enumerable.Repeat(0, 3)));
        list.InsertRange(index: 0, transformation(Enumerable.Range(0, 3).Select(x => x - 3)));
        list.InsertRange(index: list.Count, transformation(tail));

        for (int index = 0, expected = -3; index < list.Count; index++)
        {
          if (list[index] == 0) continue;

          Assert.AreEqual(expected++, list[index]);

          if (expected == 0) expected++;
        }
      }
    }

    [Test]
    public void Reverse()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var countBefore = list.Count;
        var enumerator = list.GetEnumerator();

        list.Reverse();

        AssertReversed();

        if (countBefore > 0)
          Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());

        list.Reverse(startIndex: 0, length: list.Count);
        list.Reverse();

        AssertReversed();

        _ = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() => list.Reverse());

        void AssertReversed()
        {
          for (var index = 0; index < list.Count; index++)
          {
            Assert.AreEqual(list.Count - index, list[index]);
          }

          Assert.AreEqual(countBefore, list.Count);
        }
      }
    }

    [Test]
    public void Sort()
    {
      var random = new Random();

      foreach (var list in CreateVariousFilledLocalLists())
      {
        var count = list.Count;
        list.Clear();

        for (var index = 0; index < count; index++)
          list.Add(random.Next());

        var enumerator = list.GetEnumerator();

        list.UnstableSort();

        if (count > 0) Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());

        AssertItemsInOrder(Assert.LessOrEqual);

        list.UnstableSort(comparison: (x, y) => y.CompareTo(x));

        AssertItemsInOrder(Assert.GreaterOrEqual);

        list.UnstableSort(); // bring back the order

        var comparer = new ReversedIntComparer();
        list.UnstableSort(index: 0, length: count, comparer);
        Assert.AreEqual(count > 1, comparer.ComparisonCount > 0);

        AssertItemsInOrder(Assert.GreaterOrEqual);

        var resultingList = list.ResultingList();

        CollectionAssert.AreEqual(resultingList.OrderByDescending(x => x), resultingList);

        Assert.Throws<InvalidOperationException>(() => list.UnstableSort());
        Assert.Throws<InvalidOperationException>(() => list.UnstableSort((x, y) => 0));
        Assert.Throws<InvalidOperationException>(() => list.UnstableSort(0, count, Comparer<int>.Default));

        void AssertItemsInOrder(Action<int, int> assertion)
        {
          for (var index = 1; index < count; index++)
          {
            var prev = list[index - 1];
            var next = list[index];

            assertion(prev, next);
          }

          list.AllFreeSlotsAreClear();
        }
      }

      {
        var bigList = new LocalList2<int>();

        for (var index = 0; index < 100; index++)
          bigList.Add(random.Next(0, 100) - 1000);
        for (var index = 0; index < 10; index++)
          bigList.Add(index);
        for (var index = 0; index < 100; index++)
          bigList.Add(random.Next(0, 100) + 1000);

        // ReSharper disable once AssignNullToNotNullAttribute
        Assert.Throws<ArgumentNullException>(
          () => bigList.UnstableSort(comparison: null));

        // ReSharper disable once AssignNullToNotNullAttribute
        Assert.Throws<ArgumentNullException>(
          () => bigList.UnstableSort(comparer: null));
        // ReSharper disable once AssignNullToNotNullAttribute
        Assert.Throws<ArgumentNullException>(
          () => bigList.UnstableSort(0, bigList.Count, comparer: null));

        bigList.UnstableSort(index: 0, length: 100, Comparer<int>.Default);
        bigList.UnstableSort(index: 105, length: 100, Comparer<int>.Default);

        var resultingList = bigList.ResultingList();
        CollectionAssert.AreEqual(resultingList.OrderBy(x => x), resultingList);
      }

      {
        var smallList = new LocalList2<int>(capacity: 8);
        smallList.Add(1);
        smallList.Add(3); //
        smallList.Add(2); //
        smallList.Add(4);
        smallList.Add(6); //
        smallList.Add(7); //
        smallList.Add(5); //
        smallList.Add(8);

        smallList.UnstableSort(index: 2, length: 2, Comparer<int>.Default);
        smallList.UnstableSort(index: 4, length: 3, Comparer<int>.Default);

        var resultingList = smallList.ResultingList();
        CollectionAssert.AreEqual(resultingList.OrderBy(x => x), resultingList);
      }
    }

    private sealed class ReversedIntComparer : IComparer<int>
    {
      public int Compare(int x, int y)
      {
        ComparisonCount++;
        return y.CompareTo(x);
      }

      public int ComparisonCount;
    }

    [Test]
    public new void ToString()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var listText = list.ToString();
        var expectedText = "[" + string.Join(", ", list.ResultingList()) + "]";

        Assert.AreEqual(expectedText, listText);

        Assert.Throws<InvalidOperationException>(() => _ = list.ToString());
      }
    }

    #region Test helpers

    [NotNull] private static readonly int[] CapacitiesToTest =
    {
      0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 15, 16, 24
    };

    private static IEnumerable<LocalList2<int>> CreateVariousFilledLocalLists()
    {
      foreach (var capacity in CapacitiesToTest)
      {
        for (var count = 0; count <= capacity + 1; count++)
        {
          var list = new LocalList2<int>(capacity);
          if (list.Capacity != capacity) continue;

          for (var index = 1; index <= count; index++)
          {
            list.Add(index);
          }

          yield return list;
        }
      }

      // some special cases
      yield return new LocalList2<int>(capacity: 1, forceUseArray: true);

      const int largeCapacity = ushort.MaxValue * 2;
      yield return new LocalList2<int>(largeCapacity);

      var largeList2 = new LocalList2<int>(largeCapacity);
      var largeCount = largeList2.Capacity / 2 + 100;

      for (var index = 1; index <= largeCount; index++)
      {
        largeList2.Add(index);
      }

      yield return largeList2;
    }

    #endregion
  }
}