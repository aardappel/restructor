using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;

class Node
{
    List<Node> l = null;
    internal NodeType t = null;
    internal string err = null;

    internal Type clrt = null;

    internal bool refactorused = false;

    internal Node(NodeType _t) { t = _t; }
    internal Node(NodeType _t, Node a) : this(_t) { Add(a); }
    internal Node(NodeType _t, Node a, Node b) : this(_t, a) { Add(b); }

    internal int Arity() { return l == null ? 0 : l.Count; }
    internal Node At(int i) { Sanity(); return l[i]; }
    internal void Insert(int i, Node n) { l.Insert(i, n); }
    internal void Remove(int pos) { l.RemoveAt(pos); }

    internal bool Exists(Predicate<Node> fun)
    {
        if (l != null) foreach (var n in l) if(fun(n)) return true;
        return false;
    }

    internal bool Exists(Func<Node, int, bool> fun)
    {
        var i = 0;
        if (l != null) foreach (var n in l) if (fun(n, i++)) return true;
        return false;
    }

    internal void ForEach(Action<Node> fun)
    {
        if (l != null) foreach (var n in l) fun(n);
    }

    internal void ForEach(Action<Node, int> fun)
    {
        var i = 0;
        if (l != null) foreach (var n in l) fun(n, i++);
    }

    internal bool ExistsNodeFun(PredicateNodeFun fun, Function f)
    {
        return fun(this, f) || (l != null && l.Exists(n => n.ExistsNodeFun(fun, f)));
    }

    internal bool ExistsNode(Predicate<Node> fun)
    {
        return fun(this) || (l != null && l.Exists(n => n.ExistsNode(fun)));
    }

    internal void ForAllNodes(Action<Node> fun)
    {
        fun(this);
        ForEach(n => n.ForAllNodes(fun));
    }

    internal void IfFunction(Action<Function> f)
    {
        var ft = t as Function;
        if (ft!=null) f(ft);
    }

    internal void IfBuiltin(Action<Builtin> f)
    {
        var bi = t as Builtin;
        if (bi!=null) f(bi);
    }

    internal void IfPlaceHolder(Action<PlaceHolder> f)
    {
        var ph = t as PlaceHolder;
        if (ph!=null) f(ph);
    }

    internal void Sanity()
    {
        /* FIXME: remove*/
        Debug.Assert(!(t is Function) ||
                      ((Function)t).undeclared ||
                      (l != null && l.Count == ((Function)t).args.Count) ||
                      (l == null && 0 == ((Function)t).args.Count));
    }

    internal bool Eq(Node o)
    {
        var i = 0;
        return t == o.t && Arity()==o.Arity() && (Arity()==0 || l.TrueForAll(n => n.Eq(o.At(i++))));
    }

    internal void Add(Node a)
    {
        (l ?? (l = new List<Node>())).Add(a);
    }

    internal void CopyOver(Node o)
    {
        l = o.l;
        t = o.t;
        err = o.err;
        clrt = o.clrt;
        Sanity();
    }

    internal void Register(Dictionary<TwoNode, SameNodeSet> rd, Function f)
    {
        Sanity();
        refactorused = false;
        ForEach((n, i) =>
        {
            var tn = new TwoNode { a = this, b = n, domf = f, pos = i };
            SameNodeSet sns;
            if (!rd.TryGetValue(tn, out sns)) rd[tn] = sns = new SameNodeSet();
            sns.l.Add(tn);

            n.Register(rd, f);
        });
    }

    public override string ToString()
    {
        var cr = new CodeRenderText();
        RenderCode(cr, null);
        return cr.s;
    }

    internal void ConvertToUnparsed()
    {
        var u = new Unparsed();
        u.name = ToString();
        l = null;
        t = u;
    }

    internal void RenderCodeChildren(CodeRender cr, string lb = "(", string rb = ")", int start = 0)
    {
        cr.Text(lb, 0, Program.bracketspacing);
        if (l != null) for (var i = start; i<Arity(); i++)
        {
            At(i).RenderCode(cr, this);
            if (i != l.Count - 1) cr.Text(",", 0, Program.commaspacing);
        }
        cr.Text(rb, Program.bracketspacing, 0);
    }

    internal void RenderCode(CodeRender cr, Node nparent)
    {
        cr.Push(this);
        t.RenderCode(cr, this, nparent);
        cr.Pop(this);
    }

    internal void GenCode(ILGenerator g, Function f, Type coerceto)
    {
        var gen = t.GenCode(g, f, this, coerceto);

        if(Program._debug && gen != typeof(void))
        {
            g.Emit(OpCodes.Dup);
            g.Emit(OpCodes.Call, typeof(Runtime).GetMethod("__dbg", new Type[] { gen }));
        }

        NodeType.Coerce(gen, coerceto, g);
    }

    internal void TypeCheck(Function f, Type expected = null)
    {
        clrt = t.TypeCheck(this, f, expected);
    }

    internal Variant FindVariant(Variant variants)
    {
        if (variants == null)
            return null;
        if (Exists((n, i) => n.clrt != variants.caller.At(i).clrt))
            return FindVariant(variants.next);
        return variants;
    }

    internal void CollectSideEffects(Dictionary<NodeType, SideEffect> sedb)
    {
        ForEach(n => n.CollectSideEffects(sedb));
        t.CollectSideEffects(sedb, this);
    }
}
