using System;
using System.Linq;
using System.Reflection;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
    public class Constraint
    {
        public Func<object, double, bool> Check; public string Name; public double Tol; public object Expected;
        public Constraint Within(double tol) { Tol = tol; return this; }
    }
    public static class Is
    {
        static bool Num(object o) => o is IConvertible && !(o is string) && !(o is bool) && !(o is Enum);
        static double D(object o) => Convert.ToDouble(o);
        public static Constraint EqualTo(object e) => new Constraint { Name = "EqualTo " + e, Expected = e, Check = null };
        public static Constraint GreaterThan(object e) => new Constraint { Name = "> " + e, Expected = e };
        public static Constraint GreaterThanOrEqualTo(object e) => new Constraint { Name = ">= " + e, Expected = e };
        public static Constraint LessThan(object e) => new Constraint { Name = "< " + e, Expected = e };
        public static Constraint LessThanOrEqualTo(object e) => new Constraint { Name = "<= " + e, Expected = e };
        public static Constraint True => new Constraint { Name = "True", Expected = true };
        public static Constraint False => new Constraint { Name = "False", Expected = false };
        public static bool Eval(object actual, Constraint c)
        {
            var n = c.Name;
            if (n.StartsWith("EqualTo")) return Num(actual) && Num(c.Expected) ? Math.Abs(D(actual) - D(c.Expected)) <= c.Tol : Equals(actual, c.Expected);
            if (n.StartsWith(">=")) return D(actual) >= D(c.Expected);
            if (n.StartsWith(">")) return D(actual) > D(c.Expected);
            if (n.StartsWith("<=")) return D(actual) <= D(c.Expected);
            if (n.StartsWith("<")) return D(actual) < D(c.Expected);
            return Equals(actual, c.Expected);
        }
    }
    public static class Assert
    {
        public static void That(object actual, Constraint c, string message = null)
        {
            if (!Is.Eval(actual, c)) throw new AssertionException((message ?? "") + " expected " + c.Name + (c.Tol > 0 ? " +/-" + c.Tol : "") + " but was " + actual);
        }
    }
}
