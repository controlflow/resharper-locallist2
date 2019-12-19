using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

public struct StringBox {
  public string Value;
}

[RankColumn]
public class ArraysBenchmark {
  private int[] intArray;
  private string[] stringArray;
  private StringBox[] stringBoxArray;

  [Params(1000, 10000)]
  public int Count;

  [GlobalSetup]
  public void Setup() {
    intArray = new int[Count];
    stringArray = new string[Count];
    stringBoxArray = new StringBox[Count];
  }

  [Benchmark]
  public void IntArray() {
    var array = intArray;
    for (var index = 0; index < array.Length; index++) {
      array[index] = 42;
    }
  }

  [Benchmark]
  public void StringArray() {
    var array = stringArray;
    for (var index = 0; index < array.Length; index++) {
      array[index] = "42";
    }
  }

  [Benchmark]
  public void StringBoxArray() {
    var array = stringBoxArray;
    for (var index = 0; index < array.Length; index++) {
      array[index].Value = "42";
    }
  }
}

public class Program {
  public static void Main(string[] args) {
    BenchmarkRunner.Run<ArraysBenchmark>();
    Example.Run();
  }
}