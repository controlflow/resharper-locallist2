using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable AssignmentIsFullyDiscarded

namespace JetBrains.Util.Tests
{
  // todo: test version overflows
  // todo: test version increment at enlargement

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
        Console.WriteLine($"count={list.Count}, capacity={list.Capacity}");
        if (list.Count != 0 || list.Capacity != 1) continue;

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
    public void AddCountIndex()
    {
      foreach (var count in CapacitiesToTest.Concat(new[] { 70000 }))
      {
        var list = new LocalList2<byte>();
        Assert.AreEqual(0, list.Count);

        var bytes = NonZeroBytes(count).ToArray();
        foreach (var x in bytes) list.Add(x);

        Assert.AreEqual(count, list.Count);
        Assert.IsTrue(list.AllFreeSlotsAreClear());

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(bytes[index], list[index]);
        }

        Console.WriteLine($"count={list.Count}, capacity={list.Capacity}");

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[list.Count]);

        var resultingList = list.ResultingList();
        _ = list.Count; // do not throws

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
    public void MutableStructEnumerator01()
    {
      foreach (var capacity in CapacitiesToTest)
      {
        Console.WriteLine(capacity);

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
        Console.WriteLine($"count={list.Count}, capacity={list.Capacity}");
        if (list.Count != 1 || list.Capacity != 4) continue;

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

        using var enumerator2 = resultingList.GetEnumerator(); // reused `this`
        using var enumerator3 = resultingList.GetEnumerator(); // not reused one

        Assert.IsTrue(ReferenceEquals(resultingList, enumerator2));
        Assert.IsFalse(ReferenceEquals(enumerator2, enumerator3));

        for (var index = 1; index <= list.Count; index++)
        {
          Assert.IsTrue(enumerator2.MoveNext());
          Assert.AreEqual(index, enumerator2.Current);
        }

        Assert.IsFalse(enumerator2.MoveNext());

        for (var index = 1; index <= list.Count; index++)
        {
          Assert.IsTrue(enumerator3.MoveNext());
          Assert.AreEqual(index, enumerator3.Current);
        }

        Assert.IsFalse(enumerator3.MoveNext());
      }
    }

    [Test]
    public void MutableStructEnumerator03()
    {
      foreach (var list in CreateVariousFilledLocalLists())
      {
        // todo: test this
        foreach (ref var item in list)
        {
          item = 42;
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

        //Console.WriteLine($"count={list.Count}, capacity={list.Capacity}");
        //if (list.Count != 9 || list.Capacity != 16) continue;

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