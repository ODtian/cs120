// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
var e = new D();
public interface A
{
    public void a();
}

public static class BExtension
{
    public static void b(this B bi)
    {
        bi.b();
        // Console.WriteLine("B.b");
    }
}
public interface B : A
{
    void A.a()
    {
        Console.WriteLine("A.a");
        b();
    }

    void b()
    {
        Console.WriteLine("B.b");
    }

    void c();
}

public interface C : B
{
    void B.b()
    {
        Console.WriteLine("C.c");
    }
}

public class D : B
{
    public void c()
    {
        Console.WriteLine("D.b");
    }
}

public class E : D, C
{
}
