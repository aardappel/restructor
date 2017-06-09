using System;

class Unparsed : NodeType
{
    internal string parseerr = null;
    internal int errorpos = 0;

    override internal void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        cr.EditBox(name, n);
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        n.err = parseerr;
        return null;
    }
}
