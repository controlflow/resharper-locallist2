using JetBrains.Annotations;
using System.Diagnostics;

namespace JetBrains.Util
{
  [DebuggerDisplay("Length = 0")]
  public static class EmptyArray<T>
  {
    [NotNull]
    public static readonly T[] Instance = new T[0];
  }
}