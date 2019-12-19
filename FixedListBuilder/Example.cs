using JetBrains.Util.DataStructures.Collections;

class Example
{
  public static void Run()
  {
    FixedList.Builder<int> xs = new FixedList.ListOf4<int>();
    xs.Append(42, ref xs);

  }
}