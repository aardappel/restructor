using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Diagnostics;
using real = System.Double;

class BuiltinOp : NodeType
{
    internal int precedence = 0;
    internal int flags = 0;

    internal const int no_precedence = 1;
    internal const int right_assoc = 2;
    internal const int unary_only = 4;
    internal const int unary_and_binary = 8;
    internal const int int_result = 16;
    internal const int int_to_int = 32;
    internal const int is_add = 64;

    bool NeedsParens(Node p, Node n)
    {
        if (n.Arity() != 2 || p == null) return false;
        var pop = p.t as BuiltinOp;
        return (pop != null &&
                (pop.flags & no_precedence) == 0 &&
                (
                    pop.precedence > precedence ||      // parent op has higher prec: (A+B)*C
                    p.Arity() == 1 ||                   // parent is unary: -(A+B)
                    (
                        // we are opposite-factored on same prec: C-(A+B)
                        pop.precedence == precedence &&
                        ((pop.flags & right_assoc) != 0 ? p.At(0) : p.At(1)) == n
                    )
                ));
    }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        const double operatorscale = 1.3;
        string altop = null;

        switch (name)
        {
            case "!=": altop = "\u2260"; break;
            case "<=": altop = "\u2264"; break;
            case ">=": altop = "\u2265"; break;
            case "==": altop = "\u2A75"; break;
            case "*": altop = "\u2A2F"; break;
            case "/": altop = "\u00F7"; break;
            case "!": altop = "\u00AC"; break;
            case "|>": altop = "\u22B3"; break;
        }

        if (NeedsParens(nparent, n)) cr.Text("(", 0, Program.bracketspacing);

        if (name == "__apply")
        {
            n.At(0).RenderCode(cr, n);
            cr.Scale(operatorscale, () => cr.Text("!", 0, 0));
            if (n.Arity() > 1) n.RenderCodeChildren(cr, "(", ")", 1);
        }
        else if (name == "|>")
        {
            n.At(1).RenderCode(cr, n);
            cr.AltNext(altop);
            cr.Scale(operatorscale,
                () => cr.Text("|>", Program.operatorspacing, Program.operatorspacing));
            n.At(0).RenderCode(cr, n);
        }
        else if (name == "__list")
        {
            n.RenderCodeChildren(cr, "[", "]");
        }
        else if (n.Arity() == 1)
        {
            cr.AltNext(altop);
            cr.Scale(operatorscale,
                () => cr.Text(name, Program.operatorspacing, Program.operatorspacing));
            n.At(0).RenderCode(cr, n);
        }
        else if (n.Arity() == 2)
        {
            n.At(name == "=" ? 1 : 0).RenderCode(cr, n);
            cr.AltNext(altop);
            cr.Scale(operatorscale,
                () => cr.Text(name, Program.operatorspacing, Program.operatorspacing));
            n.At(name == "=" ? 0 : 1).RenderCode(cr, n);
        }
        else Debug.Assert(false);

        if (NeedsParens(nparent, n)) cr.Text(")", Program.bracketspacing, 0);
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        switch (name)
        {
            case ";":
                n.At(0).GenCode(g, f, typeof(void));
                n.At(1).GenCode(g, f, null);
                break;

            case "=":
                var ph = n.At(1).t as PlaceHolder;
                n.At(0).GenCode(g, f, ph.clrt);
                if (requested != typeof(void)) g.Emit(OpCodes.Dup);
                if (ph.isfreevar) g.Emit(OpCodes.Stsfld, ph.curfb);
                else g.Emit(OpCodes.Starg, (short)ph.GetCLRArg(f));
                if (requested == typeof(void)) return requested;
                break;

            case "|>":
            case "__apply":
                n.At(0).GenCode(g, f, null);
                var type = n.At(0).clrt;
                var targs = type.GetGenericArguments();
                for(var i = 1; i<n.Arity(); i++) n.At(i).GenCode(g, f, targs[i-1]);
                g.Emit(OpCodes.Callvirt, type.GetMethod("Invoke"));
                break;

            case "__list":
                g.Emit(OpCodes.Newobj, n.clrt.GetConstructor(new Type[0]));
                n.ForEach(cn =>
                {
                    g.Emit(OpCodes.Dup);
                    cn.GenCode(g, f, n.clrt.GetGenericArguments()[0]);
                    g.Emit(OpCodes.Call, n.clrt.GetMethod("Add"));
                });
                break;

            default:
                n.ForEach(cn => cn.GenCode(g, f, (flags & int_result) != 0 ? typeof(int) : n.clrt));
                switch (name)
                {
                    case "+":  if (n.clrt != typeof(string)) g.Emit(OpCodes.Add);
                               else g.Emit(OpCodes.Call, typeof(Runtime).GetMethod("__concat"));
                               break;
                    case "-":  g.Emit(n.Arity() == 2 ? OpCodes.Sub : OpCodes.Neg);
                               break;
                    case "*":  g.Emit(OpCodes.Mul);
                               break;
                    case "/":  g.Emit(OpCodes.Div);
                               break;
                    case "==": g.Emit(OpCodes.Ceq);
                               break;
                    case "!=": g.Emit(OpCodes.Ceq);
                               g.Emit(OpCodes.Ldc_I4_0);
                               g.Emit(OpCodes.Ceq);
                               // TODO: reorg args instead, and/or generate branch
                               break;
                    case "<":  g.Emit(OpCodes.Clt);
                               break;
                    case "<=": g.Emit(OpCodes.Cgt);
                               g.Emit(OpCodes.Ldc_I4_0);
                               g.Emit(OpCodes.Ceq);
                               break;
                    case ">":  g.Emit(OpCodes.Cgt);
                               break;
                    case ">=": g.Emit(OpCodes.Clt);
                               g.Emit(OpCodes.Ldc_I4_0);
                               g.Emit(OpCodes.Ceq);
                               break;
                    case "!":  g.Emit(OpCodes.Ldc_I4_0);
                               g.Emit(OpCodes.Ceq);
                               break;
                    default:   Debug.Assert(false);
                               break;
                }
                break;
        }
        return n.clrt;
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        switch (name)
        {
            case "|>":
            case "__apply":
                var ats = new Type[n.Arity() - 1];
                for (var i = 1; i < n.Arity(); i++)
                {
                    n.At(i).TypeCheck(f);
                    if ((ats[i - 1] = n.At(i).clrt) == null) return null;
                }

                Type gt = null;
                switch(n.Arity() - 1)
                {
                    case 0: gt = typeof(Action); break;
                    case 1: gt = typeof(Action<>).MakeGenericType(ats); break;
                    case 2: gt = typeof(Action<,>).MakeGenericType(ats); break;
                    case 3: gt = typeof(Action<,,>).MakeGenericType(ats); break;
                    default:
                        // TODO: can we do this more elegantly? is bounded by generic return types
                        // of builtins, so not a big deal
                        Debug.Assert(false); break;
                }
                n.At(0).TypeCheck(f, gt);

                var type = n.At(0).clrt;
                if (type == null) return null;

                var targs = type.GetGenericArguments();
                var nargs = NumFuncArgs(type, targs.Length);
                if (nargs < 0)
                {
                    TypeError(n.At(0), "function value");
                    return null;
                }

                ArgCheck(n, nargs + 1);

                return nargs==targs.Length ? typeof(void) : targs[targs.Length - 1];
        }

        n.ForEach(cn => cn.TypeCheck(f));

        switch (name)
        {
            case ";":
                return n.At(1).clrt;

            case "=":
                return SubType(n.At(0), (n.At(1).t as PlaceHolder).clrt);

            case "__list":
            {
                Type ut = null;
                n.ForEach(cn => ut = TypeUnion(ut, cn.clrt));
                var lut = typeof(List<>);
                return lut.MakeGenericType(new Type[] { ut });
            }

            default:
                if ((flags & int_to_int) != 0)
                {
                    return SubType(n.At(0), typeof(int));
                }
                else
                {
                    Type ut = null;
                    if (n.Exists(cn =>
                    {
                        ut = TypeUnion(ut, cn.clrt);

                        // TODO: comparison operators must also work on pointers, by value? etc
                        if (!IsSubType(cn.clrt, typeof(real)))
                        {
                            if ((flags & is_add) != 0)
                            {
                                if (!IsSubType(cn.clrt, typeof(string)))
                                {
                                    TypeError(cn, "numeric or string");
                                    return true;
                                }
                            }
                            else
                            {
                                TypeError(cn, "numeric");
                                return true;
                            }
                        }
                        return false;
                    })) return null;
                    return ut;
                }
        }
    }

    internal override void CollectSideEffects(Dictionary<NodeType, SideEffect> sedb, Node n)
    {
        switch (name)
        {
            case "=":
                var se = n.At(1).t.GetSideEffect(sedb);
                se.write = true;
                break;
        }
    }
}
