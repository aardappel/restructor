using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

class Selection
{
    internal Object selected = null;
    internal Object hover = null;
    internal StackPanel selectedui = null;
    internal StackPanel hoverui = null;

    MainWin w;

    internal Selection(MainWin _w) { w = _w; }

    internal void HoverMove(Point p)
    {
        if (hoverui != null && hoverui != selectedui) hoverui.Background = null;
        hoverui = null;
        hover = null;

        var htr = VisualTreeHelper.HitTest(w, p);
        if (htr == null) return;

        var fe = htr.VisualHit as Visual;
        if (fe == null) return;

        while (fe != null)
        {
            if (fe is StackPanel)
            {
                var sp = fe as StackPanel;
                var n = sp.Tag;
                if (n == null) return;
                hover = n;
                hoverui = sp;
                if (hoverui != selectedui) hoverui.Background = Brushes.LightCyan;
                return;
            }
            fe = VisualTreeHelper.GetParent(fe) as Visual;
        }
    }

    bool FindHoverUI(DependencyObject o, Object n)
    {
        if (o is StackPanel)
        {
            var sp = o as StackPanel;
            if (sp.Tag == n)
            {
                hoverui = sp;
                hover = n;
                return true;
            }
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            if (FindHoverUI(VisualTreeHelper.GetChild(o, i), n))
                return true;
        return false;
    }

    internal void Selected()
    {
        if (selectedui != null) selectedui.Background = null;

        if (hoverui != null)
        {
            hoverui.Background = Brushes.SkyBlue;
            hoverui.Focusable = true;
            Keyboard.Focus(hoverui);
        }
        else
        {
            Keyboard.ClearFocus();
        }

        if (selected != null && selected != hover &&
            selected is Node && (selected as Node).t is Unparsed)
        {
            var tb = selectedui.Children[0] as TextBox;
            var hn = hover;
            w.Replace(selected as Node, tb.Text);
            if (hn != null)
            {
                FindHoverUI(w, hn);
                Selected();
            }
            return;
        }

        selected = hover;
        selectedui = hoverui;
    }

    internal void ReSelectEditBox()
    {
        if (selected == null && hover != null && hover is Node && (hover as Node).t is Unparsed)
        {
            selected = hover;
            selectedui = hoverui;
        }
    }

    internal void DeSelect()
    {
        selected = null;
        selectedui = null;
        hover = null;
        hoverui = null;
    }

    internal void SetEditMode(string start = null)
    {
        if (selected == null) return;
        var sel = selected as Node;

        string txt;
        if (sel != null)
        {
            if (sel.t is Unparsed) return;
            sel.ConvertToUnparsed();
            txt = sel.t.name;
        }
        else if (selected is NodeType)
        {
            txt = (selected as NodeType).name;
        }
        else
        {
            return;
        }

        selectedui.Children.Clear();

        var cr = new CodeRenderGUI(w);
        cr.EditBox(txt, selected);

        var tb = cr.lasttextbox;
        if (start != null) { tb.Text = start; tb.CaretIndex = 1; }
        else tb.SelectAll();
        selectedui.Children.Add(tb);
        tb.Loaded += (s, e) => { Keyboard.Focus(tb); };
    }

    internal void HandleKey(Key k)
    {
        switch (k)
        {
            case Key.Enter:
                SetEditMode();
                break;
        }
    }
}