using System;
using System.Collections.Generic;
using real = System.Double;

public interface IRestructorCode
{
    string __main();
}

public class Runtime
{
    public static string[] __commandlineargs = {};

    //public static void __init() { System.Diagnostics.Debugger.Launch(); }

    public static void __dbg(int i) { System.Diagnostics.Debug.WriteLine("dbg: " + i); }
    public static void __dbg(real r) { System.Diagnostics.Debug.WriteLine("dbg: " + r); }
    public static void __dbg(IntPtr i) { System.Diagnostics.Debug.WriteLine("dbg: <FP>"); }
    public static void __dbg(Object o) { System.Diagnostics.Debug.WriteLine("dbg: " + o); }

    public static string __itoa(int i) { return i.ToString(); }
    public static string __ftoa(real r) { return r.ToString(); }
    public static string __concat(string a, string b) { return a + b; }

    public static int ftoi(real r) { return (int)r; }
    public static real sqrt(real r) { return Math.Sqrt(r); }

    public static int atoi(string a) { int r; int.TryParse(a, out r); return r; }
    public static real atof(string a) { real r; real.TryParse(a, out r); return r; }

    public static string getargs(int i)
    {
        return i<__commandlineargs.Length ? __commandlineargs[i] : "";
    }

    public static Func<List<T>, List<S>> map<T, S>(Func<T, S> f)
    {
        return (List<T> l) =>
        {
            var o = new List<S>();
            foreach (var e in l) o.Add(f(e));
            return o;
        };
    }

    public static Func<List<T>, A> reduce<T, A>(A s, Func<A, T, A> f)
    {
        return (List<T> l) =>
        {
            foreach (var e in l) s = f(s, e);
            return s;
        };
    }

    public static T generictest<T>(T x) { return x; }

    //public static R apply<V, R>(V v, Func<V, R> f) { return f(v); }

    public static Game g = null;

    public static void game(string title, Action<real> update, Action<real> draw)
    {
        g = new Game(title, update, draw);
        g.Run();
    }

    public static void rendersprite(string name, real x, real y, real scale, real rot)
    {
        g.RenderSprite(name, x, y, scale, rot);
    }
}