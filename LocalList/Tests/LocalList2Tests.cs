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
  // todo: test version overflows
  // todo: test version increment at enlargement

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

          if (forceArray)
            Assert.AreEqual(capacity, list.Capacity);
          else
            Assert.LessOrEqual(capacity, list.Capacity);
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

    // todo: indexer setter tests
    [Test]
    public void AddCountIndex()
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
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[list.Count]);

        var resultingList = list.ResultingList();
        _ = list.Count; // do not throws

        Assert.AreEqual(list.Count, resultingList.Count);
        Assert.Throws<InvalidOperationException>(() => _ = list.ResultingList());

        // the same APIs via IList<T> interface
        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(bytes[index], resultingList[index]);
        }

        Assert.Throws<CollectionReadOnlyException>(() => resultingList.Add(42));

        // results obtained checks
        if (list.Count > 0)
          Assert.Throws<InvalidOperationException>(() => _ = list[0]);

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
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var oldCapacity = list.Capacity;
        var countBefore = list.Count;
        var enumeratorBefore = list.GetEnumerator();

        list.TrimExcess();

        Assert.IsTrue(list.AllFreeSlotsAreClear());

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(list[index], index + 1);
        }

        Assert.LessOrEqual(list.Capacity, oldCapacity);
        Assert.LessOrEqual(list.Count, list.Capacity);
        Assert.AreEqual(countBefore, list.Count);
        Assert.DoesNotThrow(() => enumeratorBefore.MoveNext());

        var enumeratorBefore2 = list.GetEnumerator();

        list.EnsureCapacity(oldCapacity);

        Assert.AreEqual(oldCapacity, list.Capacity);
        Assert.AreEqual(countBefore, list.Count);
        Assert.DoesNotThrow(() => enumeratorBefore2.MoveNext());

        list.EnsureCapacity(0);
        Assert.AreEqual(oldCapacity, list.Capacity);

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(list[index], index + 1);
        }

        _ = list.ResultingList();

        Assert.Throws<InvalidOperationException>(() => list.TrimExcess());
        Assert.Throws<InvalidOperationException>(() => list.EnsureCapacity(0));
      }
    }

    // todo: CopyTo()
    // todo: Insert()
    // todo: AddRange()
    // todo: ToString()

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

      var largeCapacity = ushort.MaxValue * 2;
      yield return new LocalList2<int>(largeCapacity);

      var largeList2 = new LocalList2<int>(largeCapacity);
      for (var index = 1; index <= largeList2.Capacity / 2; index++)
      {
        largeList2.Add(index);
      }

      yield return largeList2;
    }

    #endregion
  }
}