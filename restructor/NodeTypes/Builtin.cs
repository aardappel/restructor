using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

class Builtin : NodeType
{
    internal MethodInfo clrf = null;

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        base.RenderCode(cr, n, nparent);
        n.RenderCodeChildren(cr);
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        Dictionary<string, Type> bindings = null;
        n.ForEach((cn, i) =>
        {
            var gent = clrf.GetParameters()[i].ParameterType;
            BindGPs(gent, cn.clrt, f, ref bindings, false);
            cn.GenCode(g, f, gent);
        });
        if (bindings != null)
        {
            var gas = clrf.GetGenericArguments();
            var i = 0;
            foreach (var ga in gas) gas[i++] = bindings[ga.Name];
            var bound = clrf.MakeGenericMethod(gas);
            g.Emit(OpCodes.Call, bound);
        }
        else
        {
            g.Emit(OpCodes.Call, clrf);
        }
        return n.clrt;
    }

    internal static void BindGPs(Type at, Type ct, Function f,
                                 ref Dictionary<string, Type> bindings, bool funcisequaltoaction)
    {
        if (at.IsGenericParameter && !ct.IsGenericParameter)
        {
            if (!(bindings ?? (bindings = new Dictionary<string, Type>())).ContainsKey(at.Name))
                bindings[at.Name] = ct;
        }
        else if (at.IsGenericType && ct.IsGenericType)
        {
            var atas = at.GetGenericArguments();
            var ctas = ct.GetGenericArguments();
            var atn = atas.Length;
            var ctn = ctas.Length;

            if (funcisequaltoaction)
            {
                atn = NumFuncArgs(at, atn);
                ctn = NumFuncArgs(ct, ctn);
                if (atn != ctn || (atn < 0 && at.Name != ct.Name)) return;
                if (atn < 0) atn = atas.Length;
            }
            else
            {
                if (atn != ctn || at.Name != ct.Name) return;
            }

            for (var j = 0; j<atn; j++)
            {
                BindGPs(atas[j], ctas[j], f, ref bindings, false);
            }
        }
    }

    internal static Type ReplaceGPs(Type t, Dictionary<string, Type> bindings)
    {
        if (bindings != null)
        {
            if (t.IsGenericParameter)
            {
                Type bt;
                if (bindings.TryGetValue(t.Name, out bt)) return bt;
            }
            else if (t.IsGenericType)
            {
                var gas = t.GetGenericArguments();
                var j = 0;
                foreach (var ga in gas) gas[j++] = ReplaceGPs(ga, bindings);
                return t.GetGenericTypeDefinition().MakeGenericType(gas);
            }
        }
        return t;
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        Dictionary<string, Type> bindings = null;

        if (expected != null)
        {
            BindGPs(clrf.ReturnType, expected, f, ref bindings, true);
        }

        if (ArgCheck(n, clrf.GetParameters().Length))
        {
            var i = 0;
            foreach (var pi in clrf.GetParameters())
            {
                var cn = n.At(i++);
                var pt = ReplaceGPs(pi.ParameterType, bindings);
                cn.TypeCheck(f, pt);
                if (cn.clrt != null)
                {
                    BindGPs(pt, cn.clrt, f, ref bindings, false);
                    pt = ReplaceGPs(pt, bindings);    // TODO: bit unoptimal
                    SubType(cn, pt);
                }
            }
        }

        return ReplaceGPs(clrf.ReturnType, bindings);
    }

    internal override void CollectSideEffects(Dictionary<NodeType, SideEffect> sedb, Node n)
    {
        // TODO: put side effect attributes to builtins, and check them here
    }
}

