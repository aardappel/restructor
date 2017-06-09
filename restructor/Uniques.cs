using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using real = System.Double;

class Uniques
{
    internal Dictionary<string, Builtin> builtins = new Dictionary<string, Builtin>();
    internal Dictionary<string, BuiltinOp> builtinops = new Dictionary<string, BuiltinOp>();

    Dictionary<int, Int> inthash = new Dictionary<int, Int>();
    Dictionary<string, String> strhash = new Dictionary<string, String>();
    Dictionary<real, Real> flthash = new Dictionary<real, Real>();

    internal BuiltinOp applyop, listop, tupleop;

    internal Uniques()
    {
        var mis = typeof(Runtime).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public |
                                             BindingFlags.Instance | BindingFlags.Static);
        foreach (var mi in mis)
        {
            var pis = mi.GetParameters();
            Debug.WriteLine("method: " + mi.Name + ":" + pis.Length);
            builtins[mi.Name] = new Builtin { name = mi.Name, clrf = mi };
        }

        applyop = GetOp("__apply", 1000, 0);
        tupleop = GetOp("__tuple", 0, BuiltinOp.no_precedence);
        listop = GetOp("__list", 0, BuiltinOp.no_precedence);
    }

    internal Int GetInt(int i)
    {
        Int it;
        if (inthash.TryGetValue(i, out it)) return it;
        return inthash[i] = new Int(i);
    }

    internal Real GetReal(real d)
    {
        Real dt;
        if (flthash.TryGetValue(d, out dt)) return dt;
        return flthash[d] = new Real(d);
    }

    internal String GetStr(string s)
    {
        String st;
        if (strhash.TryGetValue(s, out st)) return st;
        return strhash[s] = new String(s);
    }

    internal BuiltinOp GetOp(string name, int p, int flags)
    {
        BuiltinOp op;
        if (builtinops.TryGetValue(name, out op)) return op;
        return builtinops[name] = new BuiltinOp { name = name, precedence = p, flags = flags };
    }
}