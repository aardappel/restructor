using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

class PlaceHolder : NodeType
{
    internal Type clrt = null;

    internal bool isfreevar = true;     // FIXME: detect properly
    internal FieldBuilder curfb = null;

    internal string pherr = null;

    internal int GetCLRArg(Function f)
    {
        var i = 0;
        for (Function fp = f; fp != null; fp = null /*fp.parent*/)  // TODO: cleanup
        {
            i = 0;
            foreach (var arg in fp.args)
            {
                if (arg == this) return i; else i++;
            }
            foreach (var fv in fp.freevars)
            {
                if (!fv.isfreevar) { if (fv == this) return i; else i++; }  // TODO: needed?
            }
            /*
            if (fp.args.Exists(arg => ++i > 0 && arg == this) ||
                fp.freevars.Exists(arg => ++i > 0 && arg == this)) break;
                */
        }
        Debug.Assert(/*i > 0*/false);
        return i;//i - 1;
    }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.WeightNext(700);
        cr.Text(name, Program.namespacing, Program.namespacing);
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        if(isfreevar) g.Emit(OpCodes.Ldsfld, curfb);
        else g.Emit(OpCodes.Ldarg, (short)GetCLRArg(f));
        return clrt;
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        if (undeclared)
        {
            var ph = f.LookupVar(name);
            if (ph == null)
            {
                n.err = "unknown variable: " + name;
            }
            else
            {
                n.t = ph;
                undeclared = false;
                return ph.TypeCheck(n, f, expected);
            }
        }
        return clrt;
    }

    internal override void CollectSideEffects(Dictionary<NodeType, SideEffect> sedb, Node n)
    {
        GetSideEffect(sedb);
    }
}

