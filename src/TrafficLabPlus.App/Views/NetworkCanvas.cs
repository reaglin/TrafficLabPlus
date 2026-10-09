using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

public enum CanvasMode
{
    Select,
    AddSignal,
    AddEnd,
    AddRoad,
}

/// <summary>
/// The network as a plain drawing: roads as grey bands as wide as their lanes, signals as green
/// dots, roundabouts as rings, road ends as small squares. Click to choose something, drag an
/// intersection or a road end to move it, and in the Add modes click to place one or to join two.
/// The page draws the real thing; this is for finding and arranging what to edit.
/// </summary>
public sealed class NetworkCanvas : FrameworkElement
{
    private const double Edge = 36;
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush Pave = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xC0, 0xC1)));
    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(0x17, 0x20, 0x1C)));
    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(0x4F, 0x5C, 0x56)));
    private static readonly Brush Sign = Freeze(new SolidColorBrush(Color.FromRgb(0x0F, 0x6B, 0x4A)));
    private static readonly Brush Amber = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0xA4, 0x00)));
    private static readonly Brush Ground = Freeze(new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xF8)));
    private static readonly Pen GridPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xE5))), 1));

    private StudySession? _session;
    private double _scale = 1, _minX, _minY, _ox, _oy;
    private string? _dragId;
    private Point _downAt;
    private bool _dragging;
    private string? _roadFrom;
    private Point _mouse;

    public NetworkCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Arrow;
    }

    public StudySession? Session
    {
        get => _session;
        set
        {
            _session = value;
            Refit();
        }
    }

    public CanvasMode Mode { get; private set; } = CanvasMode.Select;

    /// <summary>The chosen node or road: <c>node:I1</c> or <c>link:L1</c>; null for nothing.</summary>
    public string? Selected { get; private set; }

    public event EventHandler? SelectionChanged;

    public event EventHandler? ModeChanged;

    /// <summary>A sentence for the status line: what happened, or why it could not.</summary>
    public event EventHandler<string>? Message;

    public void SetMode(CanvasMode mode)
    {
        Mode = mode;
        _roadFrom = null;
        Cursor = mode == CanvasMode.Select ? Cursors.Arrow : Cursors.Cross;
        ModeChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void Select(string? what)
    {
        if (Selected == what)
        {
            return;
        }

        Selected = what;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    /// <summary>Fits the drawing to the network again (after a change that moved its edges).</summary>
    public void Refit()
    {
        if (_dragging)
        {
            return;
        }

        Study? s = _session?.Study;
        if (s is null || s.Nodes.Count == 0 || ActualWidth < 2 * Edge || ActualHeight < 2 * Edge)
        {
            InvalidateVisual();
            return;
        }

        double minX = s.Nodes.Min(n => n.X), maxX = s.Nodes.Max(n => n.X);
        double minY = s.Nodes.Min(n => n.Y), maxY = s.Nodes.Max(n => n.Y);
        double w = Math.Max(maxX - minX, 50), h = Math.Max(maxY - minY, 50);
        _scale = Math.Min((ActualWidth - 2 * Edge) / w, (ActualHeight - 2 * Edge) / h);
        _minX = minX;
        _minY = minY;
        _ox = (ActualWidth - w * _scale) / 2;
        _oy = (ActualHeight - h * _scale) / 2;
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Refit();
    }

    private Point ToScreen(double x, double y) => new((x - _minX) * _scale + _ox, (y - _minY) * _scale + _oy);

    private (double X, double Y) ToWorld(Point p) => ((p.X - _ox) / _scale + _minX, (p.Y - _oy) / _scale + _minY);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Ground, null, new Rect(RenderSize));
        Study? s = _session?.Study;
        if (s is null)
        {
            return;
        }

        DrawGrid(dc);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        // labels are placed after everything is drawn, most important first, and one that would
        // land on another is left out (a chosen item's label always shows)
        var labels = new List<(string Text, Point At, Brush Colour, double Size, Typeface Face, bool Below, bool Must)>();

        foreach (StudyLink l in s.Links)
        {
            if (s.Node(l.A) is not { } a || s.Node(l.B) is not { } b)
            {
                continue;
            }

            Point pa = ToScreen(a.X, a.Y), pb = ToScreen(b.X, b.Y);
            double width = 4 + 3 * l.Lanes;
            if (Selected == "link:" + l.Id)
            {
                dc.DrawLine(new Pen(Amber, width + 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, pa, pb);
            }

            dc.DrawLine(new Pen(Pave, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, pa, pb);
            var mid = new Point((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
            bool chosenRoad = Selected == "link:" + l.Id;
            if (chosenRoad || (pb - pa).Length > 120)
            {
                labels.Add((l.Title + " · " + l.Lanes + (l.Lanes == 1 ? " lane" : " lanes"), mid, chosenRoad ? Ink : Muted, 11, Face, false, chosenRoad));
            }
        }

        if (Mode == CanvasMode.AddRoad && _roadFrom is not null && s.Node(_roadFrom) is { } from)
        {
            dc.DrawLine(new Pen(Amber, 3) { DashStyle = DashStyles.Dash }, ToScreen(from.X, from.Y), _mouse);
        }

        foreach (StudyNode n in s.Nodes)
        {
            Point p = ToScreen(n.X, n.Y);
            bool chosen = Selected == "node:" + n.Id || _roadFrom == n.Id;
            if (chosen)
            {
                dc.DrawEllipse(null, new Pen(Amber, 4), p, 15, 15);
            }

            switch (n.Type)
            {
                case StudyNode.SignalType:
                    dc.DrawEllipse(Sign, new Pen(Brushes.White, 2), p, 9, 9);
                    break;
                case StudyNode.Roundabout:
                    dc.DrawEllipse(Brushes.White, new Pen(Sign, 4), p, 9, 9);
                    break;
                default:
                    dc.DrawRectangle(Ink, new Pen(Brushes.White, 1.5), new Rect(p.X - 5, p.Y - 5, 10, 10));
                    break;
            }

            string label = n.Type == StudyNode.End ? n.Label : (string.IsNullOrWhiteSpace(n.Short) ? n.Label : n.Short);
            labels.Insert(n.Type == StudyNode.End ? labels.Count : 0,
                (label, new Point(p.X, p.Y + 13), n.Type == StudyNode.End ? Muted : Ink, n.Type == StudyNode.End ? 11 : 12, n.Type == StudyNode.End ? Face : Bold, true, chosen));
        }

        var taken = new List<Rect> { DrawScale(dc, dpi) };
        foreach (var l in labels.OrderByDescending(l => l.Must))
        {
            Text(dc, l.Text, l.At, l.Colour, l.Size, l.Face, dpi, l.Below, taken, l.Must);
        }
    }

    private void DrawGrid(DrawingContext dc)
    {
        // a line every 500 ft, so distances can be judged by eye
        double step = Units.Metres(500) * _scale;
        if (step < 12)
        {
            return;
        }

        Point origin = ToScreen(Math.Floor(_minX / Units.Metres(500)) * Units.Metres(500), Math.Floor(_minY / Units.Metres(500)) * Units.Metres(500));
        for (double x = origin.X; x < ActualWidth; x += step)
        {
            dc.DrawLine(GridPen, new Point(x, 0), new Point(x, ActualHeight));
        }

        for (double y = origin.Y; y < ActualHeight; y += step)
        {
            dc.DrawLine(GridPen, new Point(0, y), new Point(ActualWidth, y));
        }
    }

    private Rect DrawScale(DrawingContext dc, double dpi)
    {
        double px = Units.Metres(500) * _scale;
        var start = new Point(12, ActualHeight - 14);
        dc.DrawLine(new Pen(Ink, 2), start, new Point(start.X + px, start.Y));
        dc.DrawLine(new Pen(Ink, 2), start, new Point(start.X, start.Y - 5));
        dc.DrawLine(new Pen(Ink, 2), new Point(start.X + px, start.Y), new Point(start.X + px, start.Y - 5));
        Rect label = Text(dc, "500 ft (grid squares)", new Point(start.X + px + 8, start.Y - 8), Muted, 11, Face, dpi, below: true, taken: null, must: true, left: true);
        return Rect.Union(label, new Rect(start.X, start.Y - 6, px, 8));
    }

    private Rect Text(DrawingContext dc, string text, Point at, Brush colour, double size, Typeface face, double dpi, bool below,
                             List<Rect>? taken, bool must, bool left = false)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, colour, dpi);
        double x = left ? at.X : at.X - ft.Width / 2, y = below ? at.Y : at.Y - ft.Height - 6;
        x = Math.Max(3, Math.Min(x, ActualWidth - ft.Width - 3));   // kept inside the drawing
        var halo = new Rect(x - 2, y, ft.Width + 4, ft.Height);
        if (taken is not null)
        {
            if (!must && taken.Any(r => r.IntersectsWith(halo)))
            {
                return Rect.Empty;
            }

            taken.Add(halo);
        }

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xD8, 0xF7, 0xF9, 0xF8)), null, halo, 2, 2);
        dc.DrawText(ft, new Point(x, y));
        return halo;
    }

    // ------------------------------------------------------------ the mouse

    private string? NodeAt(Point p)
    {
        Study? s = _session?.Study;
        return s?.Nodes.Select(n => (n, d: (ToScreen(n.X, n.Y) - p).Length)).Where(t => t.d <= 14).OrderBy(t => t.d).Select(t => t.n.Id).FirstOrDefault();
    }

    private string? LinkAt(Point p)
    {
        Study? s = _session?.Study;
        if (s is null)
        {
            return null;
        }

        string? best = null;
        double bestD = 10;
        foreach (StudyLink l in s.Links)
        {
            if (s.Node(l.A) is not { } a || s.Node(l.B) is not { } b)
            {
                continue;
            }

            double d = DistanceToSegment(p, ToScreen(a.X, a.Y), ToScreen(b.X, b.Y));
            if (d < bestD)
            {
                bestD = d;
                best = l.Id;
            }
        }

        return best;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        Vector ab = b - a;
        double t = ab.LengthSquared == 0 ? 0 : Math.Clamp(Vector.Multiply(p - a, ab) / ab.LengthSquared, 0, 1);
        return (p - (a + t * ab)).Length;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (_session is null)
        {
            return;
        }

        Point p = e.GetPosition(this);
        string? node = NodeAt(p);
        switch (Mode)
        {
            case CanvasMode.AddSignal:
            case CanvasMode.AddEnd:
                {
                    (double x, double y) = ToWorld(p);
                    StudyNode? added = null;
                    string type = Mode == CanvasMode.AddSignal ? StudyNode.SignalType : StudyNode.End;
                    _session.Edit(null, () => added = StudyEdits.AddNode(_session.Study, type, x, y));
                    SetMode(CanvasMode.Select);
                    Select("node:" + added!.Id);
                    Message?.Invoke(this, $"Added \"{added.Label}\". Now join it to the network: Add road, then click it and another place.");
                    return;
                }

            case CanvasMode.AddRoad:
                if (node is null)
                {
                    Message?.Invoke(this, "Click an intersection or a road end to start the road (Esc to stop adding).");
                    return;
                }

                if (_roadFrom is null)
                {
                    _roadFrom = node;
                    Message?.Invoke(this, "Now click where the road goes to.");
                    InvalidateVisual();
                    return;
                }

                string from = _roadFrom;
                // tried on a copy first, so a road that cannot be built leaves no undo step behind
                if (StudyEdits.AddLink(StudyJson.Copy(_session.Study), from, node).Why is { } why)
                {
                    Message?.Invoke(this, why + " Click another place, or press Esc.");
                    _roadFrom = null;
                    InvalidateVisual();
                    return;
                }

                (StudyLink? Link, string? Why) result = (null, null);
                _session.Edit(null, () => result = StudyEdits.AddLink(_session.Study, from, node));

                SetMode(CanvasMode.Select);
                Select("link:" + result.Link!.Id);
                Message?.Invoke(this, $"Added a road. Set its name, lanes and speed in the form below the drawing.");
                return;
        }

        if (node is not null)
        {
            Select("node:" + node);
            _dragId = node;
            _downAt = p;
            CaptureMouse();
            return;
        }

        Select(LinkAt(p) is { } link2 ? "link:" + link2 : null);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.GetPosition(this);
        if (Mode == CanvasMode.AddRoad && _roadFrom is not null)
        {
            InvalidateVisual();
        }

        if (_dragId is null || _session?.Study.Node(_dragId) is not { } n || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!_dragging && (_mouse - _downAt).Length < 4)
        {
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            _session.BeginQuiet("node:" + n.Id + "/position");
        }

        (double x, double y) = ToWorld(_mouse);
        n.X = Math.Round(x, 1);
        n.Y = Math.Round(y, 1);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        if (_dragging && _dragId is not null)
        {
            _dragging = false;
            _session?.EndQuiet("node:" + _dragId + "/position");
            Refit();
        }

        _dragId = null;
        _dragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Mode != CanvasMode.Select)
        {
            SetMode(CanvasMode.Select);
            Message?.Invoke(this, "Stopped adding.");
            e.Handled = true;
        }
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
