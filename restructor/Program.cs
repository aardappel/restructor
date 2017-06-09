using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

class SideEffect
{
    internal NodeType id;
    internal bool write;
}

class Program
{
    internal Function mf = null;
    internal Uniques uniques = new Uniques();

    internal const int bracketspacing = 1;
    internal const int commaspacing = 3;
    internal const int equalsspacing = 10;
    internal const int namespacing = 1;
    internal const int operatorspacing = 2;

    internal Program()
    {
        New();
    }

    internal void New()
    {
        mf = new Function { name = "__main" };
        mf.root = new Parser("\"Hello, World!\"", this).Parse(mf);

        Validate();     // normally called by TreeChanged() instead.
    }

    Node GenVar(Function f)
    {
        string s;
        for (var i = 0; ; i++)
        {
            s = GenName(false, i);
            for (Function fp = f; fp != null; fp = fp.parent)
                if (fp.args.Exists(arg => arg.name == s))
                    goto keepgoing;
            break;
            keepgoing: ;
        }

        var ph = new PlaceHolder { name = s };
        f.args.Add(ph);
        return new Node(ph);
    }

    internal Function GenFun(List<string> parsednames = null)
    {
        for (var i = 0; ; i++)
        {
            string id = GenName(true, i);
            if (parsednames != null && parsednames.Contains(id)) continue;
            if (mf.ExistsFun(of => of.name == id)) continue;
            return new Function { name = id };
        }
    }

    string GenName(bool funname, int gennames)
    {
        var basechar = funname ? 'a' : 'A';
        if (gennames < 26) return ((char)(basechar + gennames)).ToString();
        Debug.Assert(gennames < 26 * 26);
        return ((char)(basechar + gennames / 26 - 1)).ToString() +
               ((char)(basechar + gennames % 26)).ToString();
    }

    internal Function FindNodeParent(Node n)
    {
        Function rf = null;
        mf.ExistsNodeFun((on, f) => on == n && (rf = f) != null);
        return rf;
    }

    internal string Restructor()
    {
        if (CodeProblems())
        {
            return "Cannot restruct code while there are errors / edits.";
        }

        bool cull_unused_args = false;
        if (cull_unused_args)
        {
            // If by means of editing a function argument (or free variable) has become unused, it
            // can be removed. This also removes all caller argument values, which is contentious,
            // since it may be code the programmer cares about, so ideally should be a separate pass
            // from Restructor where it shows you graphically in the editor what is dead code.
            // Until we have that, we just delete it, to ensure the Restructor algoritm is maximally
            // efficient.
            // FIXME: also, this must not remove parameters needed for the function-value arguments
            // to e.g. game().
            for (;;)
            {
                bool modified = false;
                mf.ForAllFuncs(f =>
                {
                    f.freevars.RemoveAll(ph => !f.ExistsNode(n => n.t == ph));
                    // Have to use a regular loop to ensure element is removed within iteration.
                    for (int i = 0; i < f.args.Count; i++)
                    {
                        var ph = f.args[i];
                        if (!f.ExistsNode(n => n.t == ph))
                        {
                            // FIXME: this also removes code with side effects, which is clearly NOT
                            // ok. A side effect argument should be pulled out of the argument list
                            // and made into a seperate statement.
                            f.args.RemoveAt(i);
                            mf.ForAllNodes(n =>
                            {
                                if (n.t == f)
                                {
                                    n.Remove(i);
                                    modified = true;
                                }
                            });
                            i--;
                        }
                    }
                });
                if (!modified) break;
            }
        }

        for (; ; )
        {
            Debug.WriteLine("Starting Pass");

            // Iterate thru all code, and add every possible TwoNode you find into a map from each
            // possible kind of TwoNode to a list of occurrences. (collecting structurally
            // equivalent copies in the code).
            // For example, if the code is `(1+2)*(1+3)`, then parent `+` with child `1` (at index
            // 0) will be the highest occurring TwoNode.

            var rd = new Dictionary<TwoNode, SameNodeSet>(new TwoNodeEqualityComparer());

            mf.ForAllRoots((n, f) => n.Register(rd, f));

            // Add all map items where the list has at least 2 items to an overal list, and sort
            // that by occurrences.

            var lsns = new List<SameNodeSet>();
            foreach (KeyValuePair<TwoNode, SameNodeSet> kvp in rd)
                if (kvp.Value.l.Count > 1)
                    lsns.Add(kvp.Value);

            lsns.Sort(new SameNodeSetOrderComparer());

            lsns.RemoveAll(sns =>
            {
                // Any node may only participate in one TwoNode, since a refactor is destructive.
                // Combinations that didn't get to participate can be picked up by next passes.
                sns.l.RemoveAll(tn =>
                {
                    bool used = tn.a.refactorused || tn.b.refactorused;
                    // have to do this while checking, because of add(add(add(...))) situations
                    tn.a.refactorused = tn.b.refactorused = true;
                    return used;
                });
                return sns.l.Count <= 1;
            });

            // Nothing to refactor, we're done!

            if (lsns.Count == 0) break;

            lsns.Sort(new SameNodeSetOrderComparer());

            // Go through the overal list in order of number of occurrences:

            foreach (var sns in lsns)
            {
                var a = sns.l[0].a;
                var b = sns.l[0].b;
                var pos = sns.l[0].pos;

                // Create a new function that will have the TwoNode as body. Generate placeholders
                // for all children of the parent that are not the given child, and all children of
                // the child.
                // So here the function would be: `f(x) = 1 + x`
                var f = GenFun();
                f.root = new Node(a.t);

                // Find the lowest common ancestor function that dominates all occurrences, so
                // function is defined as local as possible.Make that the parent function. (this
                // could be skipped if all functions were global).
                Function parent = null;
                foreach (var tn in sns.l) parent = tn.domf.LCA(parent);

                Debug.WriteLine("Make Function " + f.name + " : " + sns.l.Count + " => " +
                                parent.name);

                parent.Add(f);

                for (var i = 0; i < a.Arity(); i++)
                {
                    if (i == pos)
                    {
                        var nb = new Node(b.t);
                        b.ForEach(n => nb.Add(GenVar(f)));
                        f.root.Add(nb);
                    }
                    else
                    {
                        f.root.Add(GenVar(f));
                    }
                }

                // if the child was already a placeholder, it becomes a free variable of the new
                // function, similarly if either node is a function, then its free vars are added
                // to the new function.
                b.IfPlaceHolder(ph => f.freevars.Add(ph));
                a.IfFunction(of => f.AddFreeVarsFrom(of));
                b.IfFunction(of => f.AddFreeVarsFrom(of));

                // if the child wasn't the last child: (because if it is the last child, the order
                // of evaluation vs the other children doesn't change, so no special action is
                // needed!)
                if (pos + 1 < a.Arity())
                {
                    // Find all side-effects in all children of the child. This takes the entire
                    // call graph into account from this node.
                    var bdb = new Dictionary<NodeType, SideEffect>();
                    b.t.CollectSideEffects(bdb, b);

                    // If there are any side - effecs, for each following child, for each of its
                    // occurrences, check if the two children are order dependent.
                    // E.g. `f(a = 1, a)` or `f(a, a = 1)` are order dependent, and we have to
                    // ensure to retain the evaluation order. `f(a = 1, b)` is not order dependent.
                    if (bdb.Count > 0) for (int p = pos + 1; p < a.Arity(); p++)
                    {
                        foreach (var tn in sns.l)
                        {
                            // TODO: could instead compare against b's dict rather than create
                            var adb = new Dictionary<NodeType, SideEffect>();
                            tn.a.At(p).CollectSideEffects(adb);
                            if (adb.Count > 0) foreach (var bkvp in bdb)
                            {
                                SideEffect ase;
                                if (adb.TryGetValue(bkvp.Key, out ase))
                                {
                                    var bse = bkvp.Value;
                                    if (ase.write || bse.write)
                                    {
                                        // Found an order-dependent occurrence.
                                        // TODO: should really allow for non-se callers to call a
                                        // pure version of function
                                        goto have_se;
                                    }
                                }
                            }
                        }
                        continue;
                        have_se:
                        foreach (var tn in sns.l)
                        {
                            Function nof = GenFun();
                            parent.Add(nof);
                            nof.root = tn.a.At(p);
                            tn.a.Remove(p);
                            tn.a.Insert(p, new Node(
                                new FunctionValue { reff = nof, name = nof.name }));
                            // FIXME: free vars?
                        }
                        var apply = new Node(uniques.applyop, f.root.At(p));
                        f.root.Remove(p);
                        f.root.Insert(p, apply);
                    }
                }

                f.root.Sanity();

                // Now replace all occurrences with our new function, so our expression becomes
                // `f(2) * f(3)`.

                foreach (var tn in sns.l)
                {
                    Debug.Assert(tn.a.Arity() + tn.b.Arity() - 1 == f.args.Count);
                    Debug.Assert(tn.a.At(pos) == tn.b);

                    tn.a.t = f;
                    tn.a.Remove(pos);

                    tn.b.ForEach((n, j) => tn.a.Insert(pos + j, n));
                    tn.a.Sanity();
                }

                // If any arguments are equal accross all occurrences, merge those arguments.
                f.MergeEqualArgs(sns.l);
            }

            // Inline all functions that have just 1 caller. This is essential, because the above
            // algorithm generates a ton of mini-functions whose body contains just one node, so
            // this inlining is responsible for making those back into the largest possible
            // functions, and shifting the boundaries of abstractions.
            // Going down to one caller is a natural consequence of the above algorithm whenever a
            // function call with multiple occurrences becomes the given child of a new function.
            // Together, these parts of the algorithm create the "emergent" behavior of finding
            // optimal function boundaries regardless of the code structure.
            InlineAll();
        }

        InlineAll();
        Validate();     // should not be needed, just incase restructor trampled on some types

        return "Restructed.";
    }

    internal void InlineAll()
    {
        do
        {
            mf.ForAllFuncs(f => {
                f.numcallers = 0;
                f.lastcaller = null;
                f.usedasfunctionvalue = false;
            });
            // can't do this out of loop since lastcaller objects may move during inline
            mf.ForAllNodes(n => n.t.InlineCount(n));
        } while (mf.ExistsFun(f => f.Inline()));

        while (mf.ExistsFun(f => f.OneOnOneInline(this))) { };
    }

    internal void Validate()
    {
        mf.ForAllFuncs(f => { f.variants = null; f.root.ForAllNodes(n => n.err = null); });
        mf.TypeCheck(null, null, null);
        mf.SubType(mf.root, typeof(string));
    }

    bool CodeProblems() {
        return mf.ExistsNode(n => {
            /*Debug.Assert(n.clrt!=null); */
            return n.err != null || n.t is Unparsed || n.clrt == null;
        });
    }

    internal static TypeBuilder _cls = null;
    internal static bool _debug = false;

    internal string GenCode(bool run, bool debug)
    {
        // should not be needed, but to be sure there's no missing types etc, to avoid nasty
        // codegen errors
        Validate();

        if (CodeProblems())
        {
            return "Cannot compile code while there are errors / edits.";
        }

        var asm = Thread.GetDomain().DefineDynamicAssembly(
                       new AssemblyName("RestructorAssembly"), AssemblyBuilderAccess.RunAndSave);
        var mod = asm.DefineDynamicModule("RestructorModule", "RestructorOutput.exe", false);
        var cls = mod.DefineType("RestructorClass", TypeAttributes.Public);
        cls.AddInterfaceImplementation(typeof(IRestructorCode));

        _cls = cls;
        _debug = debug;
        var mb = mf.GenCodeFun(typeof(string));
        _cls = null;
        _debug = false;

        cls.DefineMethodOverride(mb, typeof(IRestructorCode).GetMethod(mf.name));

        cls.CreateType();

        string ret = "";
        if (run)
        {
            var code = (IRestructorCode)asm.CreateInstance("RestructorClass");
            try
            {
                ret = code.__main();
                Debug.WriteLine("RUN: " + ret);
            }
            catch (Exception e)
            {
                ret = "exception: " + e.Message + "\n" + e.StackTrace;
            }
        }
        else
        {
            var clse = mod.DefineType("RestructorEntry", TypeAttributes.Public);

            var mmb = clse.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static,
                                        typeof(int), new Type[] { typeof(string[]) });
            var g = mmb.GetILGenerator();
            g.Emit(OpCodes.Newobj, typeof(Runtime).GetConstructor(new Type[0]));
            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Stfld, typeof(Runtime).GetField("__commandlineargs"));
            //g.Emit(OpCodes.Call, typeof(Runtime).GetMethod("__init", new Type[0]));
            g.Emit(OpCodes.Newobj, cls.GetConstructor(new Type[0]));
            g.Emit(OpCodes.Call, mb);
            g.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine",
                                                           new Type[] { typeof(string) }));
            g.Emit(OpCodes.Ldc_I4_0);
            g.Emit(OpCodes.Ret);

            clse.CreateType();

            asm.SetEntryPoint(mmb, PEFileKinds.WindowApplication);

            try
            {
                asm.Save("RestructorOutput.exe",
                         PortableExecutableKinds.Required32Bit | PortableExecutableKinds.ILOnly,
                         ImageFileMachine.I386);
                ret = "Saved Exe.";
            }
            catch
            {
                ret = "Couldn't write exe.";
            }
        }

        // TODO: maybe should only clear the mb's if the gui wants to be able to look at the
        // variants, or just call Validate() again
        mf.ForAllFuncs(f => f.variants = null);

        return ret;
    }

    void CreateArgListGui(List<PlaceHolder> l, CodeRender cr, string left, string right)
    {
        cr.Text(left, 0, bracketspacing);
        foreach (var arg in l)
        {
            cr.Push(arg);
            arg.RenderCode(cr, null, null);
            cr.Pop(arg);
            if (arg != l[l.Count - 1]) cr.Text(",", 0, commaspacing);
        }
        cr.Text(right, bracketspacing, 0);
    }

    internal void RenderCode(CodeRender cr)
    {
        cr.StartTopLevel();
        var i = 0;
        mf.ForAllFuncs(f =>
        {
            cr.Push(null);
            cr.Push(f);
            f.RenderCode(cr, null, null);
            cr.Pop(f);
            CreateArgListGui(f.args, cr, "(", ")");
            if(f.freevars.Count>0) cr.Scale(0.7, () => CreateArgListGui(f.freevars, cr, "[", "]"));
            cr.Pop(null);
            cr.SaveFunctionLHS();

            cr.Push(null);
            cr.Text("=", equalsspacing, equalsspacing);
            f.root.RenderCode(cr, null);
            cr.Pop(null);

            cr.Function(i++, f.Depth());
        });
    }
}
