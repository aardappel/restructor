using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Diagnostics;

abstract class CodeRender
{
    internal virtual void Push(Object n) { }
    internal virtual void Pop(Object n) { }

    internal virtual void Text(string text, int leftm, int rightm) { }

    internal virtual void Scale(double sc, Action a) { }

    internal virtual void AltNext(string s) { }
    internal virtual void ColNext(Brush b) { }
    internal virtual void WeightNext(int w) { }

    internal virtual void EditBox(string text, Object n) { }

    internal virtual void StartTopLevel() { }
    internal virtual void SaveFunctionLHS() { }
    internal virtual void Function(int i, int depth) { }
}

class CodeRenderGUI : CodeRender
{
    MainWin w;

    List<StackPanel> stack = new List<StackPanel>();
    List<Object> nodestack = new List<Object>();
    internal Grid topgrid = null;

    internal StackPanel lastsp = null;
    internal StackPanel lastspf = null;
    internal TextBox lasttextbox = null;

    int errnesting = 0;

    // these are set for one item, then cleared
    double scale = 0;
    string altop = null;
    Brush col = null;
    int fontweight = 0;

    internal CodeRenderGUI(MainWin _w) { w = _w; }

    StackPanel Top() { return stack.Count == 0 ? null : stack[stack.Count - 1]; }

    void SetNode(FrameworkElement fe)
    {
        if (nodestack.Count > 0) fe.Tag = nodestack[nodestack.Count - 1];
    }

    internal override void Push(Object n)
    {
        var sp = GUI.StackPanel(true);
        sp.Tag = n;
        stack.Add(sp);
        nodestack.Add(n);
        if (n != null && n is Node && (n as Node).err != null) errnesting++;
    }

    internal override void Pop(Object n)
    {
        var top = Top();
        stack.RemoveAt(stack.Count - 1);
        nodestack.RemoveAt(nodestack.Count - 1);

        FrameworkElement fe = top;
        if (n != null)
        {
            var node = n as Node;
            if (node != null)
            {
                if (node.err != null) errnesting--;

                fe.AllowDrop = true;

                string tt = null;

                if (node.err != null)
                {
                    tt = node.err;
                    fe = new Border
                    {
                        BorderBrush = Brushes.Red,
                        BorderThickness = new Thickness(2),
                        CornerRadius = new CornerRadius(2),
                        Child = top
                    };
                }
                else if (node.clrt != null && errnesting == 0)
                {
                    tt = NodeType.NiceType(node.clrt);
                }

                if (tt != null) fe.ToolTip = new ToolTip { Content = tt };
            }
        }

        if (Top() != null) Top().Children.Add(fe);

        lastsp = top;
    }

    internal override void Text(string text, int leftm, int rightm)
    {
        var tb = GUI.TextBlock(Top(), text, leftm, rightm);
        SetNode(tb);
        if (scale != 0) { tb.LayoutTransform = new ScaleTransform(scale, scale); }
        if (altop != null) { tb.Text = altop; altop = null; }
        if (col != null) { tb.Foreground = col; col = Brushes.Black; }
        if (fontweight > 0)
        {
            tb.FontWeight = FontWeight.FromOpenTypeWeight(fontweight);
            fontweight = 0;
        }
    }

    internal override void Scale(double sc, Action a) { scale = sc; a(); scale = 0; }

    internal override void AltNext(string s) { altop = s; }
    internal override void ColNext(Brush b) { col = b; }
    internal override void WeightNext(int w) { fontweight = w; }

    internal override void EditBox(string text, Object n)
    {
        var tb = GUI.TextBox(Top(), text);
        SetNode(tb);
        tb.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (n is Node) w.Replace(n as Node, tb.Text);
                else if (n is NodeType) { (n as NodeType).name = tb.Text; w.TreeChanged(); }
                e.Handled = true;
            }
        };
        lasttextbox = tb;
    }

    internal override void StartTopLevel()
    {
        topgrid = new Grid();
        topgrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topgrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topgrid.Margin = new Thickness(5);
    }

    internal override void SaveFunctionLHS() { lastspf = lastsp; }

    internal override void Function(int i, int depth)
    {
        lastspf.Margin = new Thickness { Left = depth * 20 };
        topgrid.Children.Add(lastspf);
        topgrid.Children.Add(lastsp);
        topgrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(lastspf, i);
        Grid.SetRow(lastsp, i);
        Grid.SetColumn(lastsp, 1);
    }
}

class CodeRenderText : CodeRender
{
    string lhs = "";
    internal string s = "";
    internal string program = "";

    internal override void Text(string text, int leftm, int rightm) { s += text; }
    internal override void EditBox(string text, Object n) { s += text; }
    internal override void SaveFunctionLHS() { lhs = s; s = ""; }
    internal override void Function(int i, int depth)
    {
        program += "".PadRight(depth * 4) + lhs + s + "\n";
        lhs = s = "";
    }

    internal override void Scale(double sc, Action a) { a(); }
}
