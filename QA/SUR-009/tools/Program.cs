using System;
using System.Linq;
using System.Reflection;
public static class Program
{
    public static int Main(string[] args)
    {
        int pass = 0, fail = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name.EndsWith("Tests")))
        {
            var instance = Activator.CreateInstance(type);
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute")))
            {
                try { m.Invoke(instance, null); pass++; Console.WriteLine("PASS " + m.Name); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine("FAIL " + m.Name + ": " + e.InnerException.Message); }
            }
        }
        Console.WriteLine("passed " + pass + " failed " + fail);
        return fail;
    }
}
