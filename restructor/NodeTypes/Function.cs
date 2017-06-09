using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

delegate bool PredicateNodeFun(Node n, Function f);

class Variant
{
    internal Variant next;
    internal Node caller;

    internal MethodBuilder mb = null;
}

class Function : NodeType
{
    internal Node root = null;
    internal List<PlaceHolder> args = new List<PlaceHolder>();
    internal List<PlaceHolder> freevars = new List<PlaceHolder>();

    internal Function parent = null;
    List<Function> funcs = new List<Function>();

    internal Variant variants = null;

    internal int numcallers = 0;
    internal Node lastcaller = null;
    internal bool usedasfunctionvalue = false;

    internal bool typechecked = false;

    internal void Add(Function f)
    {
        funcs.Add(f);
        f.parent = this;
    }

    internal void ForAllRoots(Action<Node, Function> fun)
    {
        fun(root, this);
        foreach (var f in funcs) f.ForAllRoots(fun);
    }

    internal void ForAllFuncs(Action<Function> fun)
    {
        fun(this);
        foreach (var f in funcs) f.ForAllFuncs(fun);
    }

    internal void ForAllNodes(Action<Node> fun)
    {
        ForAllRoots((r, _) => r.ForAllNodes(fun));
    }

    internal Function Find(Predicate<Function> fun) {
        return fun(this) ? this : funcs.Find(f => f.Find(fun) != null);
    }

    internal bool ExistsFun(Predicate<Function> fun) {
        return fun(this) || funcs.Exists(f => f.ExistsFun(fun));
    }

    internal bool ExistsNode(Predicate<Node> fun)
    {
        return root.ExistsNode(fun) || funcs.Exists(f => f.ExistsNode(fun));
    }

    internal bool ExistsNodeFun(PredicateNodeFun fun) {
        return root.ExistsNodeFun(fun, this) || funcs.Exists(f => f.ExistsNodeFun(fun));
    }

    internal void ForEachArg(Action<PlaceHolder, int> fun)
    {
        var i = 0;
        foreach (var arg in args) fun(arg, i++);
    }

    internal bool ExistsArg(Func<PlaceHolder, int, bool> fun)
    {
        var i = 0;
        foreach (var arg in args) if (fun(arg, i++)) return true;
        return false;
    }

    internal void RemoveAll(Predicate<Function> fun)
    {
        foreach (var f in funcs) f.RemoveAll(fun);
        funcs.RemoveAll(fun);
    }

    internal override void RenderCode(CodeRender cr, Node n, Node nparent)
    {
        base.RenderCode(cr, n, nparent);
        if (n == null) return;
        n.RenderCodeChildren(cr);
    }

    internal bool Inline()
    {
        if (numcallers > 1 || usedasfunctionvalue || parent == null) return false;

        if (numcallers == 0)
        {
            Debug.WriteLine("Orphan: " + name + " : " + root);
        }
        else
        {
            Debug.Assert(args.Count == lastcaller.Arity());
            foreach (var arg in args)
            {
                int num = 0;
                if (ExistsNode(n => n.t == arg && num++ > 0)) return false;
            }

            Debug.WriteLine("Inline " + name + " : " + root);

            foreach (var arg in args)
                root.ForAllNodes(n => {
                    if (n.t == arg) n.CopyOver(lastcaller.At(args.IndexOf(arg)));
                });
            ForAllFuncs(f => { foreach (var arg in args) f.freevars.Remove(arg); });

            lastcaller.CopyOver(root);
        }

        CleanUpAfterInline();
        return true;
    }

    void CleanUpAfterInline()
    {
        parent.funcs.Remove(this);

        foreach (var f in funcs)
        {
            parent.funcs.Add(f);
            f.parent = parent;
        }
    }

    internal override void InlineCount(Node n)
    {
        Debug.Assert(args != null);
        numcallers++;
        if (lastcaller != null) Debug.Assert(lastcaller.Arity() == n.Arity());
        lastcaller = n;
    }

    internal bool OneOnOneInline(Program prog)
    {
        if (args.Count != root.Arity() || usedasfunctionvalue) return false;
        if (ExistsArg((arg, i) => arg != root.At(i++).t)) return false;

        Debug.WriteLine("OneOnOne " + name + " : " + root);

        prog.mf.ForAllNodes(n => { if (n.t == this) n.t = root.t; });

        CleanUpAfterInline();
        return true;
    }

    internal int Depth() { return parent == null ? 0 : parent.Depth() + 1; }

    void Path(List<Function> l)
    {
        l.Insert(0, this);
        if (parent != null) parent.Path(l);
    }

    internal Function LCA(Function o)
    {
        if (o == null) return this;
        var la = new List<Function>(); Path(la);
        var lb = new List<Function>(); o.Path(lb);
        var n = Math.Min(la.Count, lb.Count);
        for (var i = 0; i < n; i++) if (la[i] != lb[i]) return la[i - 1];
        return la[n - 1];
    }

    internal void MergeEqualArgs(List<TwoNode> l)
    {
        for (var i = args.Count - 1; i > 0; i--) for (var j = 0; j < i; j++)
        {
            if (l.TrueForAll(tn => tn.a.At(i).Eq(tn.a.At(j))))
            {
                // does not handle free vars, but the vars we look at are all local.
                root.ForAllNodes(n => { if (n.t == args[i]) n.t = args[j]; });
                args.RemoveAt(i);
                foreach (var tn in l) tn.a.Remove(i);
                break;
            }
        }
    }

    static int numphs = 0;

    internal MethodBuilder GenCodeFun(Type coercebodyto = null)
    {
        var al = new List<Type>();
        foreach (var arg in args) al.Add(arg.clrt);
        foreach (var fv in freevars) if (!fv.isfreevar) al.Add(fv.clrt);  // TODO: ever the case?

        var mb = Program._cls.DefineMethod(
            name,
            MethodAttributes.Public |
                (parent != null ? MethodAttributes.Static : MethodAttributes.Virtual),
            coercebodyto != null ? coercebodyto : root.clrt,
            al.ToArray());
        var g = mb.GetILGenerator();

        ForEachArg((arg, i) =>
        {
            if (arg.isfreevar)
            {
                arg.curfb =
                    Program._cls.DefineField(arg.name + "_" + numphs++,
                                             arg.clrt,
                                             FieldAttributes.Static | FieldAttributes.Assembly);
                g.Emit(OpCodes.Ldarg, (short)i);
                g.Emit(OpCodes.Stsfld, arg.curfb);
            }
        });

        root.GenCode(g, this, coercebodyto);

        foreach (var arg in args) arg.curfb = null;

        g.Emit(OpCodes.Ret);

        return mb;
    }

    internal void GenCodeVal(ILGenerator g, Type clrt)
    {
        Debug.Assert(variants.next == null);
        g.Emit(OpCodes.Ldnull);
        g.Emit(OpCodes.Ldftn, variants.mb ?? (variants.mb = GenCodeFun()));
        g.Emit(OpCodes.Newobj, clrt.GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
    }

    internal override Type GenCode(ILGenerator g, Function f, Node n, Type requested)
    {
        ForEachArg((arg, i) =>
        {
            Node cn = n.At(i);
            // no coerce, as the function is specialized to the type of these exps, and arg.clrt is
            // not set to right variant yet
            cn.GenCode(g, f, null);
        });
        // TODO: will this ever not be the case?
        foreach (var fv in freevars) if (!fv.isfreevar) fv.GenCode(g, f, null, null);

        var v = n.FindVariant(variants);
        Debug.Assert(v != null);

        if (v.mb == null)
        {
            // have to redecorate with types for any variant function
            // TODO: ideally this recursion would cut off at non-variant functions... though
            // variants are likely to be leafs anyway.
            // profile if codegen on a large program ever spends much time in typecheck
            if (variants.next != null) TypeCheckFun(n);
            v.mb = GenCodeFun();
        }

        g.Emit(OpCodes.Call, v.mb);
        return n.clrt;
    }

    internal void TypeCheckFunMinimal()
    {
        typechecked = true;
        foreach (var f in funcs) f.typechecked = false;
        root.TypeCheck(this);
        // make sure orphaned functions are decorated
        foreach (var f in funcs) if (!f.typechecked) f.TypeCheckFunMinimal();
    }

    internal void TypeCheckFun(Node n)  // called from both typecheck and codegen
    {
        ForEachArg((arg, i) => { arg.clrt = n.At(i).clrt; arg.pherr = null; });
        TypeCheckFunMinimal();
        ForEachArg((arg, i) => {
            if (arg.pherr != null) n.At(i).err = "in " + name + "(): " + arg.pherr;
        });
    }

    internal override Type TypeCheck(Node n, Function f, Type expected)
    {
        if (n != null) n.ForEach(cn => cn.TypeCheck(f));

        if (undeclared)
        {
            Function uf;
            if (f == null || (uf = f.LookupFun(name)) == null)
            {
                n.err = "unknown function: " + name;
                return null;
            }

            n.t = uf;
            undeclared = false;
            return uf.TypeCheck(n, f, expected);
        }

        if (n != null && ArgCheck(n, args.Count))
        {
            var v = n.FindVariant(variants);
            if(v!=null) return v.caller.clrt;

            TypeCheckFun(n);
        }
        else
        {
            if (expected != null)       // function value
            {
                // FIXME: also need to check for variant, store list of types instead
                var targs = expected.GetGenericArguments();
                var nargs = NumFuncArgs(expected, targs.Length);
                // if this fails, it means we're passing a function value to builtin requiring a
                // List<> or so, which will give an error higher up
                if (nargs < 0)
                {
                    return null;
                }
                if (nargs != args.Count)
                {
                    // FIXME: error associated with the wrong node
                    root.err = "function value requires " + nargs + " parameters";
                    return null;
                }
                ForEachArg((arg, i) =>
                {
                    arg.clrt = targs[i];
                    arg.pherr = null;
                });
                TypeCheckFunMinimal();
                foreach (var arg in args)
                {
                    // FIXME!!
                    if (arg.pherr != null)
                    {
                        root.err = "error in body: " + arg.pherr;
                        return null;
                    }
                }
            }
            else
            {
                // either main function, an orphaned function, or wrong #of args, try and typecheck
                // as much as we can
                TypeCheckFunMinimal();
            }
        }

        variants = new Variant { next = variants, caller = n };

        return root.clrt;
    }

    internal Function LookupFun(string name)
    {
        foreach (var f in funcs) if (f.name == name) return f;
        return parent == null ? null : parent.LookupFun(name);
    }

    internal PlaceHolder LookupVar(string name)
    {
        foreach (var arg in args) if (arg.name == name) return arg;
        foreach (var fv in freevars) if (fv.name == name) return fv;
        return null;
    }

    internal void AddFreeVarsFrom(Function of)
    {
        foreach(var fv in of.freevars) if(!freevars.Contains(fv)) freevars.Add(fv);
    }

    internal override void CollectSideEffects(Dictionary<NodeType, SideEffect> sedb, Node n)
    {
        // FIXME: this is hugely expensive, only do this once per function!
        root.CollectSideEffects(sedb);
    }
}

