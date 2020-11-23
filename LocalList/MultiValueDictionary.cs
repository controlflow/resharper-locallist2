using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
// ReSharper disable MergeConditionalExpression

namespace JetBrains.Util
{
  public class MultiValueDictionary<TKey, TValue>
  {
    [NotNull] private int[] myBuckets; // 1-based index in 'myEntries', 0 - empty
    [NotNull] private Entry[] myEntries;
    [CanBeNull] private IEqualityComparer<TKey> myComparer;
    private int myFreeList = -1; // index in 'myEntries', -1 - empty
    private int myCount;

    private struct Entry
    {
      public TKey Key;
      public TValue Value;
      public int Hash;
      public int Next;
    }

    [NotNull] private static readonly Entry[] ourEmptyEntries = new Entry[1];
    [NotNull] private static readonly int[] ourEmptyBucket = new int[1];

    public MultiValueDictionary()
    {
      myBuckets = ourEmptyBucket;
      myEntries = ourEmptyEntries;
    }

    public bool ContainsKey([NotNull] TKey key)
    {
      var entries = myEntries;
      var collisions = 0;
      var hashCode = myComparer == null ? key.GetHashCode() : myComparer.GetHashCode(key);

      for (var index = myBuckets[hashCode & (myBuckets.Length - 1)] - 1;
        (uint) index < (uint) entries.Length;
        index = entries[index].Next)
      {
        ref var entry = ref entries[index];

        if (hashCode == entry.Hash)
        {
          if (myComparer == null
            ? EqualityComparer<TKey>.Default.Equals(key, entry.Key)
            : myComparer.Equals(key, entry.Key))
          {
            return true;
          }
        }

        if (collisions == entries.Length) ThrowConcurrentModification();

        collisions++;
      }

      return false;
    }

    public bool TryGetValue([NotNull] TKey key, out TValue value)
    {
      var entries = myEntries;
      var collisions = 0;
      var hashCode = myComparer == null ? key.GetHashCode() : myComparer.GetHashCode(key);

      for (var index = myBuckets[hashCode & (myBuckets.Length - 1)] - 1;
        (uint) index < (uint) entries.Length;
        index = entries[index].Next)
      {
        ref var entry = ref entries[index];

        if (hashCode == entry.Hash)
        {
          if (myComparer == null
            ? EqualityComparer<TKey>.Default.Equals(key, entry.Key)
            : myComparer.Equals(key, entry.Key))
          {
            value = entry.Value;
            return true;
          }
        }

        if (collisions == entries.Length) ThrowConcurrentModification();

        collisions++;
      }

      value = default;
      return false;
    }

    public bool Remove([NotNull] TKey key)
    {
      var entries = myEntries;
      var bucketIndex = key.GetHashCode() & (myBuckets.Length - 1);
      var entryIndex = myBuckets[bucketIndex] - 1;

      var lastIndex = -1;
      var collisionCount = 0;
      while (entryIndex != -1)
      {
        var candidate = entries[entryIndex];
        if (candidate.Key.Equals(key))
        {
          if (lastIndex != -1)
            entries[lastIndex].Next = candidate.Next;
          else
            myBuckets[bucketIndex] = candidate.Next + 1;

          entries[entryIndex] = default;

          entries[entryIndex].Next = -3 - myFreeList;
          myFreeList = entryIndex;

          myCount--;
          return true;
        }

        lastIndex = entryIndex;
        entryIndex = candidate.Next;

        if (collisionCount == entries.Length) ThrowConcurrentModification();

        collisionCount++;
      }

      return false;
    }

    public ref TValue GetOrAddKeyAndGetValueRef([NotNull] TKey key)
    {
      var entries = myEntries;
      var collisionCount = 0;
      var bucketIndex = key.GetHashCode() & (myBuckets.Length - 1);

      for (var index = myBuckets[bucketIndex] - 1;
        (uint) index < (uint) entries.Length;
        index = entries[index].Next)
      {
        if (key.Equals(entries[index].Key)) return ref entries[index].Value;
        if (collisionCount == entries.Length) ThrowConcurrentModification();

        collisionCount++;
      }

      return ref AddKey(key, bucketIndex);
    }

    public void Clear()
    {
      myCount = 0;
      myFreeList = -1;
      myBuckets = ourEmptyBucket;
      myEntries = ourEmptyEntries;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref TValue AddKey([NotNull] TKey key, int bucketIndex)
    {
      var entries = myEntries;

      int entryIndex;
      if (myFreeList != -1)
      {
        entryIndex = myFreeList;
        myFreeList = -3 - entries[myFreeList].Next;
      }
      else
      {
        if (myCount == entries.Length || entries.Length == 1)
        {
          entries = Resize();
          bucketIndex = key.GetHashCode() & (myBuckets.Length - 1);
        }

        entryIndex = myCount;
      }

      entries[entryIndex].Key = key;
      entries[entryIndex].Next = myBuckets[bucketIndex] - 1;
      myBuckets[bucketIndex] = entryIndex + 1;
      myCount++;

      return ref entries[entryIndex].Value;
    }

    [NotNull]
    private Entry[] Resize()
    {
      Assertion.Assert(
        myEntries.Length == myCount || myEntries.Length == 1,
        "myEntries.Length == myCount || myEntries.Length == 1");

      var count = myCount;
      var newSize = myEntries.Length * 2;
      if ((uint) newSize > int.MaxValue)
        throw new InvalidOperationException();

      var entries = new Entry[newSize];
      Array.Copy(myEntries, 0, entries, 0, count);

      var newBuckets = new int[entries.Length];
      while (count-- > 0)
      {
        var hashCode = entries[count].Key.GetHashCode(); // re-hashing
        var bucketIndex = hashCode & (newBuckets.Length - 1);
        entries[count].Next = newBuckets[bucketIndex] - 1;
        newBuckets[bucketIndex] = count + 1;
      }

      myBuckets = newBuckets;
      myEntries = entries;

      return entries;
    }

    [ContractAnnotation("=> halt")]
    private static void ThrowConcurrentModification()
    {
      throw new InvalidOperationException("Concurrent modification");
    }
  }
}