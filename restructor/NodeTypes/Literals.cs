using System;
using System.Windows.Media;
using System.Reflection.Emit;
using real = System.Double;

class Int : NodeType
{
    int i = 0;

    internal Int(int _i) { i = _i; name = i.ToString(); }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.ColNext(Brushes.Red); cr.Text(name, Program.namespacing, Program.namespacing);
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        g.Emit(OpCodes.Ldc_I4, i);
        return typeof(int);
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        return typeof(int);
    }
}

class Real : NodeType
{
    real d = 0;

    internal Real(real _d) { d = _d; name = d.ToString("0.0#################"); }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.ColNext(Brushes.Red);
        cr.Text(name, Program.namespacing, Program.namespacing);
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        g.Emit(OpCodes.Ldc_R8, d);
        return typeof(real);      // real
    }

    internal override Type TypeCheck(Node n, Function f, Type expected) { return typeof(real); }
}

class String : NodeType
{
    internal String(string _s) { name = _s; }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.ColNext(Brushes.Green);
        cr.Text("\"" + name + "\"", Program.namespacing, Program.namespacing);
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        g.Emit(OpCodes.Ldstr, name);
        return typeof(string);
    }

    internal override Type TypeCheck(Node n, Function f, Type expected) { return typeof(string); }
}
