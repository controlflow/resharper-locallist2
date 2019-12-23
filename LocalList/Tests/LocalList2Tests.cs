using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable ArgumentsStyleLiteral

namespace JetBrains.Util.Tests
{
  [TestFixture]
  public sealed class LocalList2Tests
  {
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
      }
    }

    [Test]
    public void AddCountIndex()
    {
      foreach (var count in CapacitiesToTest)
      {
        var list = new LocalList2<int>();
        Assert.AreEqual(0, list.Count);

        var bytes = NonZeroBytes(count).ToArray();
        foreach (var x in bytes) list.Add(x);

        Assert.AreEqual(count, list.Count);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(bytes[index], list[index]);
        }

        list.Clear();
        Assert.AreEqual(0, list.Count);
        Assert.IsTrue(list.AllFreeSlotsAreClear());
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
    public void MutableStructEnumerator01()
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
    public void MutableStructEnumerator02()
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
        using var enumerator2 = resultingList.GetEnumerator();

        for (var index = 1; index <= list.Count; index++)
        {
          Assert.IsTrue(enumerator2.MoveNext());
          Assert.AreEqual(index, enumerator2.Current);
        }

        Assert.IsFalse(enumerator2.MoveNext());
      }
    }

    [Test]
    public void Clear()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        var oldCapacity = list.Capacity;
        var enumerator = list.GetEnumerator();

        //Console.WriteLine($"count={list.Count}, capacity={list.Capacity}");

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

        Assert.Throws<CollectionReadOnlyException>(() => list.Clear());
      }
    }

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

          for (var i = 1; i <= count; i++) list.Add(i);

          yield return list;
        }
      }

      // some special cases
      yield return new LocalList2<int>(capacity: 1, forceUseArray: true);
    }
  }
}