using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace JetBrains.Util
{
  public static class CollectionUtils
  {
    /// <summary>
    /// Tries to determine the number of elements in <paramref name="enumerable"/> in <c>O(1)</c>.
    /// </summary>
    [Pure]
    public static int TryGetCountFast<T>([NotNull, NoEnumeration] this IEnumerable<T> enumerable)
    {
      switch (enumerable ?? throw new ArgumentNullException(nameof(enumerable)))
      {
        case ICollection<T> genericCollection:
          return genericCollection.Count;
        case ICollection nonGenericCollection:
          return nonGenericCollection.Count;
        case IReadOnlyCollection<T> readOnlyCollection:
          return readOnlyCollection.Count;
        default:
          return -1;
      }
    }
  }

  public static class Assertion
  {
    [Conditional("DEBUG")]
    [AssertionMethod]
    public static void Assert([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition)
    {
      if (!condition)
        throw new Exception("Assertion failed");
    }

    [Conditional("DEBUG")]
    [AssertionMethod]
    public static void Assert([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, [NotNull] string message)
    {
      if (!condition)
        throw new Exception($"Assertion failed: {message}");
    }
  }
}