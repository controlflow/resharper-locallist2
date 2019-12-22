using System;
using System.Collections.Generic;
using NUnit.Framework;

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
      foreach (var list in CreateVariousLocalLists())
      {
        var clone = new LocalList2<int>(in list, preserveCapacity: false);

        Assert.AreEqual(list.Count, clone.Count);
        Assert.LessOrEqual(clone.Capacity, list.Capacity);

        for (var index = 0; index < list.Count; index++)
        {
          Assert.AreEqual(list[index], clone[index]);
        }
      }
    }

    private static IEnumerable<LocalList2<int>> CreateVariousLocalLists()
    {
      foreach (var capacity in new[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 15, 16, 24})
      {
        for (var count = 0; count < capacity; count++)
        {
          var list = new LocalList2<int>();

          for (var i = 0; i < count; i++) list.Add(i);

          yield return list;
        }
      }

      // some special cases
      yield return new LocalList2<int>(capacity: 1, forceUseArray: true);
    }
  }
}