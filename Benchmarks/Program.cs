using System.Collections;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using JetBrains.Util;

[RankColumn, IterationCount(10000), MemoryDiagnoser]
public class LocalListBenchmarks
{
  private List<int> myList;
  private LocalList<int> myLocalList;
  private LocalList2<int> myLocalList2;

  //[Params(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 25)]
  [Params(2, 4, 6, 8, 10, 25)]
  public int Count;

  [GlobalSetup]
  public void Setup()
  {
    myList = new List<int>(capacity: Count);
    myLocalList = new LocalList<int>(capacity: Count);
    myLocalList2 = new LocalList2<int>(capacity: Count);
  }

  [IterationSetup]
  public void Clear()
  {
    myList.Clear();
    myLocalList.Clear();
    myLocalList2.Clear();
  }

  //[Benchmark]
  public void List()
  {
    var list = myList;

    for (var index = 0; index < Count; index++)
    {
      list.Add(index);
    }

    // for (var index = 0; index < Count; index++)
    // {
    //    var t = list[index];
    //    list[index] = t;
    // }

    foreach (var _ in list)
    {

    }

    foreach (var _ in (IList<int>) list)
    {

    }
  }

  //[Benchmark]
  public void LocalList()
  {
    var list = myLocalList;

    for (var index = 0; index < Count; index++)
    {
      list.Add(index);
    }

    // for (var index = 0; index < Count; index++)
    // {
    //   var t = list[index];
    //   list[index] = t;
    // }

    foreach (var _ in list)
    {

    }
  }

  [Benchmark]
  public void LocalList2()
  {
    var list = myLocalList2;

    for (var index = 0; index < Count; index++)
    {
      list.Add(index);
    }

    // for (var index = 0; index < Count; index++)
    // {
    //   var t = list[index];
    //   list[index] = t;
    // }

    foreach (var _ in list)
    {

    }
  }
}

public class Program {
  public static void Main(string[] args) {
    BenchmarkRunner.Run<LocalListBenchmarks>();
  }
}


