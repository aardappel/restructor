using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.IO;
using System.Reflection;

internal class GUI
{
    internal static StackPanel StackPanel(bool horiz)
    {
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Orientation = horiz ? Orientation.Horizontal : Orientation.Vertical
        };
    }

    internal static TextBlock TextBlock(Panel parent, string s, int leftm = 0, int rightm = 0)
    {
        var tb = new TextBlock
        {
            Text = s,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness { Left = leftm, Right = rightm }
        };
        if (parent != null) parent.Children.Add(tb);
        return tb;
    }

    internal static TextBox TextBox(Panel parent, string name, Action<string> act = null)
    {
        var tb = new TextBox { Text = name, BorderThickness = new Thickness(1.5) };
        if (act != null) tb.TextChanged += (s, e) => act(tb.Text);
        if (parent != null) parent.Children.Add(tb);
        return tb;
    }

    internal static ToolBar ToolBar(ToolBarTray parent)
    {
        var tb = new ToolBar();
        tb.Background = new SolidColorBrush(Color.FromRgb(0xBC, 0xC7, 0xD8));
        parent.ToolBars.Add(tb);
        return tb;
    }

    internal static Button Button(MainWin w, ToolBar parent, string name, ICommand cmd,
                                  bool requiressel = false, Action act = null)
    {
        var tt = new ToolTip();
        tt.Content = name;
        var bt = new Button { Content = name, ToolTip = tt };
        var bm = LoadImage(name);
        if (bm != null)
        {
            var im = new Image();
            im.Source = bm;
            im.Height = im.Width = 16;
            bt.Content = im;
        }
        bt.Command = cmd;
        if (act != null)
        {
            w.CommandBindings.Add(new CommandBinding(cmd, (s, e) => act(),
                                                          (s, e) => e.CanExecute = !requiressel ||
                                                                           w.sel.selected != null));
        }
        //bt.Click += (s, e) => act();
        if(!bt.IsEnabled) bt.Opacity = 0.5f;
        bt.IsEnabledChanged += (s, e) => bt.Opacity = (bool)e.NewValue ? 1.0f : 0.5f;
        if (parent != null) parent.Items.Add(bt);
        return bt;

        //KeyBinding OpenCmdKeyBinding =
        //               new KeyBinding(ApplicationCommands.Copy, Key.C, ModifierKeys.Control);
        //this.InputBindings.Add(OpenCmdKeyBinding);
    }

    internal static Separator TBSep()
    {
        var sep = new Separator();
        sep.SnapsToDevicePixels = true;
        sep.Background = Brushes.DarkGray;
        return sep;
    }

    internal static BitmapImage LoadImage(string name)
    {
        try
        {
            return new BitmapImage(new Uri("pack://application:,,,/Images/" + name + ".png"));
        }
        catch
        {
            return null;
        }
    }
}

internal class RsCommand : ICommand
{
    Action act;
    public RsCommand(Action _a) { act = _a; }
    public bool CanExecute(object p) { return true; }
    public void Execute(object p)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        act();
        Mouse.OverrideCursor = null;
    }
    public event EventHandler CanExecuteChanged { add { } remove { } }
}

/*
internal class RWin : Window
{
    void RenderSprite(Canvas view, Canvas sp, double scale, double x, double y)
    {
        sp.LayoutTransform = new ScaleTransform(scale, scale);
        view.Children.Add(sp);
        Canvas.SetTop(sp, x-sp.Width*scale/2);
        Canvas.SetLeft(sp, y-sp.Height*scale/2);
    }

    internal RWin()
    {
        Title = "test";
        var ship = (Canvas)System.Windows.Markup.XamlReader.Load(
                             new System.IO.StreamReader("..\\..\\Game\\spaceship.xaml").BaseStream);

        var view = new Canvas();
        RenderSprite(view, ship, 0.1, 100, 100);
        //RenderSprite(view, ship, 0.1, 100, 200);
        Content = view;
    }
}
*/

internal class MainWin : Window
{
    internal Program prog = new Program();
    internal Selection sel;

    double scale = 1.2;

    DockPanel dp = null;
    FrameworkElement lastsp = null;

    Point dragstartpoint;
    internal Node dragnode = null;   // for reference only, actual data is text

    void SetScale() { dp.LayoutTransform = new ScaleTransform(scale, scale); }

    string LoadFile(string fn)
    {
        try
        {
            var streamReader = new StreamReader(fn, System.Text.Encoding.ASCII);
            var text = streamReader.ReadToEnd();
            streamReader.Close();
            return text;
        }
        catch
        {
            return "";
        }
    }

    void TestCase()
    {
        /*
        var src =
        "2+4*3-6+1*9-2+4*3-6+1*9-5+7*6*8-2+4*3-6+1*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+7*2+4*" +
        "3-2+4*3-6+2+4*3-6+1*9-5+2+4*3-6+1*9-2+4*3-6+1*9-2+4*3-6+1*9-5+7*6*8-2+4*3-6+1*9-" +
        "5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+7*2+4*3-2+4*3-6+2+4*2+4*3-6+1*9-2+4*3-6+1*9-5+7*6*" +
        "8-2+4*3-6+1*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+7*2+4*3-2+4*3-6+2+4*3-6+1*9-5+2+4*3-" +
        "6+1*9-5+7*6*8-7*2+4*3-6+1*9-5+7*6*8-7*8-2+4*3-6+1*9-2+4*3-6+1*9-5+7*6*8-2+4*3-6+" +
        "1*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+7*2+4*3-2+4*3-6+2+4*3-6+1*9-5+2+4*3-6+1*9-5+7*" +
        "6*8-7*2+4*3-6+1*9-5+7*2+4*3-6+1*9-2+4*3-6+1*9-5+7*6*8-2+4*3-6+1*9-5+2+4*3-6+1*9-" +
        "5+7*6*8-7*6*8-7+7*2+4*3-2+4*3-6+2+4*3-6+1*9-5+2+4*3-2+4*3-6+1*9-2+4*3-6+1*9-5+7*" +
        "6*8-2+4*3-6+1*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+7*2+4*3-2+4*3-6+2+4*3-6+1*9-5+2+4*" +
        "3-6+1*9-5+7*6*8-7*2+4*3-6+1*9-5+7*6*8-7*8-7*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+1*9-" +
        "5+7*6*8-7*8-7+1*9-5+7*6*8-7*2+4*3-6+1*9-5+7*6*8-7*8-7*9-5+2+4*3-6+1*9-5+7*6*8-7*" +
        "6*8-7+1*9-5+7*6*8-7*8-7*8-7*8-7*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+1*9-5+7*6*8-7*8-" +
        "7*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+1*9-5+7*6*8-7*8-7-6+1*9-5+2+4*3-6+1*9-5+7*6*8-" +
        "7*2+4*3-6+1*9-5+7*6*8-7*8-7*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+1*9-5+7*6*8-7*8-7+7*" +
        "6*8-7*2+4*3-6+1*9-5+7*6*8-7*8-7*9-5+2+4*3-6+1*9-5+7*6*8-7*6*8-7+1*9-5+7*6*8-7*8-7";
        */

        //var src = "((1-3)+(1-4))*((2-7)+(2-6))";    // simple free vars

        var src = "game(\"test\", time => 0, time => rendersprite(\"spaceship.gif\", 50, 50, 0.5," +
                  " time)); (v = 111 => v*v; v = 6; v = 7; \"list: \" + ([1, 2, 3] |> map(x =>" +
                  " x*x) |> reduce(0, (a, x) => a+x)) + \" then \" + sqrt(10) + \" and \" +" +
                  " (((1!=0)-3)+(1-atoi(\"4\")))*((2-atoi(getargs(0)))+(2+-v)))";

        //var src = "v = 111 => v*v; v = 6; v = 7; sqrt(10) + \" and \" + (((1!=0)-3)+" +
        //          "(1-atoi(\"4\")))*((2-atoi(getargs(0)))+(2+-v))";

        //var src = "x = 2 => ((x = 3) + x) * ((x = 5) + 4)";

        //var src = "sqrt(1+2+3)+\"a\"+\"b\"";

        //var src = "generictest(16)";

        // FIXME: code executed twice... should be into arg if no side effects
        //var src = "(1-4)*(1-4)";

        // FIXME: inner x not recognized.
        //var src = "x = 0 => (y = 0 => x = x + y; y) + (y = 0 => x = x - y; y)";

        //var src = LoadFile("test.txt");

        prog.mf.root = new Parser(src, prog).Parse(prog.mf);

        prog.Validate();
        prog.Restructor();
    }

    internal void TreeChanged()
    {
        sel.DeSelect();

        prog.Validate();

        if(lastsp!=null) dp.Children.Remove(lastsp);
        var cr = new CodeRenderGUI(this);
        prog.RenderCode(cr);
        dp.Children.Add(lastsp = cr.topgrid);
        DockPanel.SetDock(lastsp, Dock.Bottom);

        SetScale();

        GC.Collect();
    }

    internal void Replace(Node n, string s)
    {
        n.CopyOver(new Parser(s, prog).Parse(prog.FindNodeParent(n)));
        TreeChanged();
    }

    internal MainWin()
    {
        sel = new Selection(this);

        TestCase();

        Title = "Restructor";
        FontFamily = new FontFamily("Consolas");

        dp = new DockPanel();

        var tbs = new ToolBarTray();
        tbs.Background = new SolidColorBrush(Color.FromRgb(0xA4, 0xB3, 0xC8));
        dp.Children.Add(tbs);
        DockPanel.SetDock(tbs, Dock.Top);

        var tb = GUI.ToolBar(tbs);
        var res = GUI.TextBox(null, "Ready.");
        GUI.Button(this, tb, "New", ApplicationCommands.New, false,
            () => { prog.New(); TreeChanged(); });
        GUI.Button(this, tb, "Open", ApplicationCommands.Open, false,
            () => MessageBox.Show("Not Implimented: Open"));
        GUI.Button(this, tb, "Save", ApplicationCommands.Save, false, () =>
        {
            var cr = new CodeRenderText();
            prog.RenderCode(cr);
            var sw = new StreamWriter("text.restruct");
            sw.Write(cr.program);
            sw.Close();
            res.Text = "Saved Restructor Code.";
        });
        GUI.Button(this, tb, "Save As", ApplicationCommands.SaveAs, false,
            () => MessageBox.Show("Not Implimented: SaveAs"));
        tb.Items.Add(GUI.TBSep());
        GUI.Button(this, tb, "Copy", ApplicationCommands.Copy, true,
            () => Clipboard.SetText(sel.selected.ToString()));
        GUI.Button(this, tb, "Paste", ApplicationCommands.Paste, true,
            () => { if (sel.selected is Node) Replace(sel.selected as Node, Clipboard.GetText()); });
        GUI.Button(this, tb, "Undo", ApplicationCommands.Undo, false,
            () => MessageBox.Show("Not Implimented: Undo"));
        tb.Items.Add(GUI.TBSep());
        GUI.Button(this, tb, "Restruct", new RsCommand(() =>
        {
            res.Text = prog.Restructor();
            TreeChanged();
        }));
        GUI.Button(this, tb, "Run", new RsCommand(
            () => { res.Text = prog.GenCode(true, false); GC.Collect(); }));
        GUI.Button(this, tb, "Save Exe", new RsCommand(
            () => { res.Text = prog.GenCode(false, false); GC.Collect(); }));
        GUI.Button(this, tb, "DBG", new RsCommand(
            () => { res.Text = prog.GenCode(true, true); GC.Collect(); }));
        GUI.Button(this, tb, "DBGS", new RsCommand(
            () => { res.Text = prog.GenCode(false, true); GC.Collect(); }));

        var tb2 = GUI.ToolBar(tbs);
        FocusManager.SetIsFocusScope(tb2, false);
        var tbsp = GUI.StackPanel(true);
        GUI.TextBox(tbsp, "<input>", s => Runtime.__commandlineargs = s.Split(' '));
        tbsp.Children.Add(res);
        tb2.Items.Add(tbsp);

        foreach (DependencyObject a in tb.Items) ToolBar.SetOverflowMode(a, OverflowMode.Never);
        foreach (DependencyObject a in tb2.Items) ToolBar.SetOverflowMode(a, OverflowMode.Never);

        TreeChanged();
        Content = dp;

        MouseWheel += (s, e) =>
        {
            scale *= 1.0 + e.Delta / 600.0;
            if(scale>2) scale = 2;
            if(scale<1) scale = 1;
            SetScale();
        };

        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount > 1) sel.SetEditMode();
            else sel.Selected();
        };

        PreviewMouseLeftButtonDown += (s, e) =>
        {
            sel.ReSelectEditBox();
            dragstartpoint = Mouse.GetPosition(this);
        };

        MouseMove += (s, e) =>
        {
            sel.HoverMove(e.GetPosition(this));

            if (e.LeftButton == MouseButtonState.Pressed && dragnode==null)
            {
                Vector diff = dragstartpoint - Mouse.GetPosition(this);

                if ((Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) &&
                    sel.selected != null)
                {
                    var seln = sel.selected as Node;
                    if (seln != null && !(seln.t is Unparsed))
                    {
                        dragnode = seln;
                        DataObject data =
                            new DataObject(DataFormats.UnicodeText, sel.selected.ToString());
                        DragDrop.DoDragDrop(sel.selectedui, data, DragDropEffects.Copy);
                        dragnode = null;
                    }
                }
            }
        };

        DragOver += (s, e) =>
        {
            sel.HoverMove(e.GetPosition(this));
            if(sel.hover == dragnode) e.Effects = DragDropEffects.None;
        };

        Drop += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                if(sel.hover!=null && sel.hover is Node)
                    Replace(sel.hover as Node, e.Data.GetData(DataFormats.UnicodeText) as string);
            }
        };

        KeyDown += (s, e) =>
        {
            switch (e.Key)
            {
                default:
                    sel.HandleKey(e.Key);
                    break;
            }
        };

        TextInput += (s, e) =>
            sel.SetEditMode(e.Text);
    }
}

internal class ReApp : Application
{
    [STAThread]
    internal static void Main()
    {
        (new ReApp()).Run(new MainWin());
    }
}
