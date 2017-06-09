using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Collections.Generic;
using real = System.Double;

abstract class NodeType
{
    internal string name = null;
    internal bool undeclared = false;

    public override string ToString() { return name; }

    internal virtual void InlineCount(Node n) {}

    internal virtual void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.Text(name, Program.namespacing, Program.namespacing);
    }

    internal virtual Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        Debug.Assert(false);
        return null;
    }

    internal abstract Type TypeCheck(Node n, Function f, Type expected);

    internal virtual void CollectSideEffects(Dictionary<NodeType, SideEffect> sedb, Node n) { }

    internal SideEffect GetSideEffect(Dictionary<NodeType, SideEffect> sedb)
    {
        SideEffect se;
        if(sedb.TryGetValue(this, out se)) return se;
        se = new SideEffect { id = this, write = false };
        sedb[this] = se;
        return se;
    }

    static internal string NiceGenericType(string s, Type[] gas, string pre, string post)
    {
        s += pre;
        var j = 0;
        foreach (var ga in gas)
        {
            s += NiceType(ga);
            j++;
            if (j != gas.Length) s += ",";
        }
        return s + post;
    }

    static internal string NiceType(Type t)
    {
        if (t == typeof(int)) return "int";
        if (t == typeof(real)) return "real";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(string)) return "string";
        if (t == typeof(object)) return "object";
        if (t == typeof(void)) return "void";

        if (t.IsGenericType)
        {
            var gas = t.GetGenericArguments();
            var s = t.Name;
            var cut = s.IndexOf('`');
            if (cut >= 0) s = s.Substring(0, cut);
            switch (s)
            {
                case "Func":
                    var post = " => " + NiceType(gas[gas.Length - 1]);
                    Array.Resize(ref gas, gas.Length - 1);
                    if (gas.Length == 1) return NiceGenericType("", gas, "", post);
                    else                 return NiceGenericType("", gas, "(", ")" + post);

                case "List": return NiceGenericType("", gas, "[", "]");
                default:     return NiceGenericType(s, gas, "<", ">");
            }
        }

        return t.ToString();
    }

    internal static int NumFuncArgs(Type t, int nargs)   // TODO: can this be done more efficiently?
    {
        if (nargs >= 1)
        {
            if (t.Name == "Func`" + nargs) return nargs - 1;
            if (t.Name == "Action`" + nargs) return nargs;
        }
        else if (t.Name == "Action") return 0;
        return -1;
    }

    static internal bool IsSubType(Type sub, Type super)
    {
        return super == null    // null means error elsewhere... don't cascade
            || sub == null
            || sub == super
            || super == typeof(object)
            || super == typeof(void)
            || (sub == typeof(int) && super == typeof(real))
            || (sub == typeof(int) && super == typeof(string))
            || (sub == typeof(real) && super == typeof(string))
            ;
    }

    internal void TypeError(Node n, string needed)
    {
        var err = name + " expects a " + needed + " type (" + NiceType(n.clrt) + " given)";
        var ph = n.t as PlaceHolder;
        if (ph != null) ph.pherr = err;
        else n.err = err;
    }

    internal Type SubType(Node n, Type super)
    {
        if (!IsSubType(n.clrt, super)) TypeError(n, NiceType(super));
        return super;
    }

    static internal Type TypeUnion(Type a, Type b)
    {
        if (a == b) return a;

        if (a == null) return b;
        if (b == null) return a;

        if (a == typeof(void)) return a;
        if (b == typeof(void)) return b;

        if (a == typeof(real) && b == typeof(int)) return a;
        if (b == typeof(real) && a == typeof(int)) return b;

        if (a == typeof(string) && b == typeof(int)) return a;
        if (a == typeof(string) && b == typeof(real)) return a;
        if (b == typeof(string) && a == typeof(int)) return b;
        if (b == typeof(string) && a == typeof(real)) return b;

        return typeof(object);
    }
    /*
    static internal Type TypeIntersect(Type a, Type b)
    {
        if (a == typeof(void) || b == typeof(void)) return null;

        if (a == b) return a;

        if (a == typeof(object)) return b;
        if (b == typeof(object)) return a;

        if (a == typeof(real) && b == typeof(int)) return b;
        if (b == typeof(real) && a == typeof(int)) return a;

        if (a == typeof(string) && b == typeof(int)) return b;
        if (a == typeof(string) && b == typeof(real)) return b;

        return null;
    }
    */
    static internal void Coerce(Type from, Type to, ILGenerator g)
    {
        if (to == null || from == to || to.IsGenericParameter)
        {
            return;
        }
        else if (from == typeof(int) && to == typeof(real))
        {
            g.Emit(OpCodes.Conv_R8);     // real
        }
        else if (from == typeof(int) && to == typeof(string))
        {
            g.Emit(OpCodes.Call, typeof(Runtime).GetMethod("__itoa"));
        }
        else if (from == typeof(real) && to == typeof(string))
        {
            g.Emit(OpCodes.Call, typeof(Runtime).GetMethod("__ftoa"));
        }
        else if (to == typeof(void))
        {
            if (from != typeof(void)) g.Emit(OpCodes.Pop);
        }
        else if (to == typeof(object))
        {
            if (from == typeof(int) || from == typeof(real))
            {
                g.Emit(OpCodes.Box);
            }
            else if (!from.IsSubclassOf(typeof(object)))
            {
                Debug.Assert(false);    // must be a value type we don't box yet?
            }
        }
        else if (to.IsGenericType && from.IsGenericType && to.Name == from.Name)
        {
            return;
        }
        else
        {
            Debug.Assert(false);       // must catch this combination in the type checker
        }
    }

    static internal bool ArgCheck(Node n, int required)
    {
        if (n.Arity() != required)
        {
            n.err = "arguments required: " + required + " (" + n.Arity() + " given)";
            return false;
        }

        return true;
    }
}

