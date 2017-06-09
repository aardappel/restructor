using System;
using System.Diagnostics;
using System.Reflection.Emit;

class FunctionValue : NodeType
{
    public Function reff = null;

    internal override void InlineCount(Node n) { reff.usedasfunctionvalue = true; }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.Text("`" + (reff != null ? reff.name : name), Program.namespacing, Program.namespacing);
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        if (undeclared)
        {
            if (f == null || (reff = f.LookupFun(name)) == null)
            {
                n.err = "unknown function: " + name;
                return null;
            }

            undeclared = false;
        }

        reff.TypeCheck(null, f, expected);
        if (reff.root.clrt == null) return null;

        if (expected != null)
        {
            var fas = expected.GetGenericArguments();
            var nargs = NumFuncArgs(expected, fas.Length);
            if (nargs < 0) return null;
            if (nargs == fas.Length) return expected;
            Debug.Assert(nargs == fas.Length - 1);

            fas[fas.Length - 1] = reff.root.clrt;
            return expected.GetGenericTypeDefinition().MakeGenericType(fas);
        }
        else
        {
            return reff.root.clrt == typeof(void)       // FIXME: do these need arg types?
                ? typeof(Action)
                : typeof(Func<>).MakeGenericType(new Type[] { reff.root.clrt });
        }

    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        reff.GenCodeVal(g, n.clrt);
        return n.clrt;
    }
}

