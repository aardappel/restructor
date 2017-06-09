using System.Collections.Generic;

struct TwoNode
{
    internal Node a, b;
    internal Function domf;
    internal int pos;
}

class SameNodeSet
{
    internal List<TwoNode> l = new List<TwoNode>();
}

class TwoNodeEqualityComparer : IEqualityComparer<TwoNode>
{
    public bool Equals(TwoNode tn1, TwoNode tn2)
    {
        return tn1.a.t == tn2.a.t &&
               tn1.b.t == tn2.b.t &&
               tn1.pos == tn2.pos &&
               tn1.a.Arity() == tn2.a.Arity() &&
               tn1.b.Arity() == tn2.b.Arity();
    }

    public int GetHashCode(TwoNode tn)
    {
        return tn.a.t.name.GetHashCode() ^
               tn.b.t.name.GetHashCode();
    }
}

class SameNodeSetOrderComparer : IComparer<SameNodeSet>
{
    public int Compare(SameNodeSet x, SameNodeSet y)
    {
        return x.l.Count < y.l.Count ? 1 : (x.l.Count > y.l.Count ? -1 : 0);
    }
}
