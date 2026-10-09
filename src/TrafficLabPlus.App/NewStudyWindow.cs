using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TrafficLabPlus.App.Views;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App;

/// <summary>
/// New study: a name, a starting layout, and the budget the page will give players (Ron,
/// 2026-10-09). Everything else can be changed afterwards; the window says so.
/// </summary>
public sealed class NewStudyWindow : Window
{
    private readonly TextBox _title = new() { Padding = new Thickness(3) };
    private readonly TextBox _place = new() { Padding = new Thickness(3) };
    private readonly TextBox _main = new() { Padding = new Thickness(3), Text = "Main Street" };
    private readonly TextBox _cross = new() { Padding = new Thickness(3), Text = "Cross Street" };
    private readonly RadioButton _four = new() { Content = "One signal, four roads (a crossroads)", IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
    private readonly RadioButton _tee = new() { Content = "One signal, three roads (a T)", Margin = new Thickness(0, 2, 0, 2) };
    private readonly RadioButton _row = new() { Content = "Signals along a main street:", Margin = new Thickness(0, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _count = new() { Width = 70 };
    private readonly TextBox _budget = new() { Width = 90, Padding = new Thickness(3), Text = StudyTemplates.SuggestedBudget.ToString(CultureInfo.CurrentCulture), HorizontalContentAlignment = HorizontalAlignment.Right };
    private readonly TextBox _author = new() { Padding = new Thickness(3) };
    private readonly TextBox _course = new() { Padding = new Thickness(3) };
    private readonly TextBlock _error = new() { Foreground = Form.Res("ErrorBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    public NewStudyWindow(AppSettings settings)
    {
        Title = "New study — TrafficLab+";
        Width = 600;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Form.Res("PageBrush");
        FontSize = 14;
        _author.Text = settings.DefaultAuthor ?? "";
        _course.Text = settings.DefaultCourse ?? "";
        for (int i = 2; i <= StudyTemplates.MaxSignals; i++)
        {
            _count.Items.Add(i);
        }

        _count.SelectedItem = 3;
        // touching the count means a row of signals: it never looks chosen while another layout is
        _count.SelectionChanged += (_, _) => _row.IsChecked = true;
        _count.GotKeyboardFocus += (_, _) => _row.IsChecked = true;
        _count.DropDownOpened += (_, _) => _row.IsChecked = true;
        AutomationProperties.SetName(_count, "How many signals along the main street");

        var p = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        p.Children.Add(new TextBlock { Text = "A new traffic study", Style = (Style)Application.Current.Resources["SectionTitle"] });
        p.Children.Add(new TextBlock
        {
            Text = "A study becomes one web page: a live simulation of your intersection that players fix on a budget. It starts from a simple layout that you then shape to match the real roads in Network — every name, lane, speed and signal timing can be changed afterwards. (Bringing in the real roads from a map arrives in the next version.)",
            Style = (Style)Application.Current.Resources["Lead"],
        });

        Field(p, "_Title", _title, "The page's heading, like \"Nova Road Traffic Lab\".");
        Field(p, "_Place", _place, "Where it is, like \"Daytona Beach, Florida\". Optional.");

        p.Children.Add(new TextBlock { Text = "Starting layout", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
        p.Children.Add(_four);
        p.Children.Add(_tee);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_row);
        row.Children.Add(_count);
        p.Children.Add(row);
        p.Children.Add(Help("Pick the closest; you can add and remove intersections and roads later. Signals are laid out a quarter mile apart."));
        Field(p, "_Main street", _main, "The street the signals are on.");
        Field(p, "C_ross street", _cross, "The street that crosses it. With more than one signal they are numbered (Cross Street 1, 2…); rename each in Network.");

        p.Children.Add(new Label { Content = "_Budget for a player's plan", Target = _budget, Padding = new Thickness(0), Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.SemiBold });
        var money = new StackPanel { Orientation = Orientation.Horizontal };
        money.Children.Add(new TextBlock { Text = "$ ", VerticalAlignment = VerticalAlignment.Center });
        money.Children.Add(_budget);
        money.Children.Add(new TextBlock { Text = " million", VerticalAlignment = VerticalAlignment.Center });
        AutomationProperties.SetName(_budget, "Budget, in millions of dollars");
        p.Children.Add(money);
        p.Children.Add(Help("What a player's plan may spend on fixes — turn lanes, extra lanes, retiming, roundabouts. LPGA's is $5 million: enough for a few real fixes, not everything. Spending less scores better; going over halves the score."));

        Field(p, "_Author", _author, "Your name, for the page's credit line. Optional; set the usual one in Settings.");
        Field(p, "C_ourse", _course, "The class it is for. Optional.");
        p.Children.Add(_error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var create = new Button { Content = "Create the study", IsDefault = true, Style = (Style)Application.Current.Resources["Primary"], Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 6, 12, 6) };
        create.Click += (_, _) => Create();
        buttons.Children.Add(create);
        buttons.Children.Add(cancel);
        p.Children.Add(buttons);
        Content = new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += (_, _) => _title.Focus();
    }

    public NewStudyRequest? Request { get; private set; }

    private void Create()
    {
        double? budget = ReadMillions(_budget.Text);
        if (string.IsNullOrWhiteSpace(_title.Text))
        {
            _error.Text = "Give the study a title — it is the heading of the page.";
            _title.Focus();
            return;
        }

        if (budget is not (>= 0.1 and <= 1000))
        {
            _error.Text = "Type the budget in millions of dollars, from 0.1 to 1000 — for example 5, or $3.5M.";
            _budget.Focus();
            return;
        }

        Request = new NewStudyRequest
        {
            Title = _title.Text.Trim(),
            Place = _place.Text,
            Author = _author.Text,
            Course = _course.Text,
            Signals = _row.IsChecked == true ? (int)_count.SelectedItem : 1,
            Tee = _tee.IsChecked == true,
            Budget = budget.Value,
            MainStreet = _main.Text,
            CrossStreet = _cross.Text,
        };
        DialogResult = true;
    }

    /// <summary>A budget as people write it: 5, 3.5, $3.5M, 3.5 million, 3,500,000 (dollars, when
    /// it is plainly too big to be millions). Null when it is not a number.</summary>
    public static double? ReadMillions(string text)
    {
        string t = text.Trim().ToLowerInvariant().Replace("$", "", StringComparison.Ordinal).Replace("million", "", StringComparison.Ordinal).Trim();
        if (t.EndsWith('m'))
        {
            t = t[..^1].Trim();
        }

        if (!double.TryParse(t, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double v) || !double.IsFinite(v))
        {
            return null;
        }

        return v >= 10_000 ? v / 1_000_000 : v;
    }

    private static void Field(Panel p, string label, TextBox box, string help)
    {
        p.Children.Add(new Label { Content = label, Target = box, Padding = new Thickness(0), Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.SemiBold });
        AutomationProperties.SetName(box, label.Replace("_", "", StringComparison.Ordinal));
        p.Children.Add(box);
        p.Children.Add(Help(help));
    }

    private static TextBlock Help(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush"), Margin = new Thickness(0, 2, 0, 0) };
}
