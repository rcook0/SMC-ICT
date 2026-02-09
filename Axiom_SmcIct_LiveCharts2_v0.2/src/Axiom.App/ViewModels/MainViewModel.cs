using System.Collections.ObjectModel;
using System.Windows.Input;
using Axiom.Core;
using Axiom.SmcIct;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace Axiom.App.ViewModels;

public sealed class MainViewModel
{
    public string Title { get; } = "SMC/ICT — Generalized Engine (v0.2: swings/BOS/CHoCH/EQ)";

    public ObservableCollection<ISeries> Series { get; } = new();
    public ObservableCollection<Axis> XAxes { get; } = new();
    public ObservableCollection<Axis> YAxes { get; } = new();
    public ObservableCollection<RectangularSection> Sections { get; } = new();

    public DrawMarginFrame DrawMarginFrame { get; } = new()
    {
        Fill = new SolidColorPaint(SKColors.Transparent),
        Stroke = new SolidColorPaint(new SKColor(50, 50, 50, 40))
    };

    public ICommand ReloadCommand { get; }

    private readonly SmcIctEngine _engine = new(new SmcIctParameters());

    public MainViewModel()
    {
        ReloadCommand = new RelayCommand(_ => LoadAndRender());
        LoadAndRender();
    }

    private void LoadAndRender()
    {
        Series.Clear();
        XAxes.Clear();
        YAxes.Clear();
        Sections.Clear();

        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_ohlcv.csv");
        var candles = CandleCsv.Load(path);

        var res = _engine.Analyze(candles, tf: Timeframes.M5);

        // Map time -> base index (5m)
        var indexOf = new Dictionary<DateTime, int>(candles.Count);
        for (int i = 0; i < candles.Count; i++) indexOf[candles[i].Time] = i;

        var ohlc = candles.Select((c, i) => new FinancialPoint(i, c.High, c.Open, c.Close, c.Low)).ToArray();

        Series.Add(new CandlesticksSeries<FinancialPoint>
        {
            Name = "Price",
            Values = ohlc,
        });

        // Signals (scatter)
        var buyPts = new List<ObservablePoint>();
        var sellPts = new List<ObservablePoint>();
        foreach (var s in res.Signals)
        {
            if (!indexOf.TryGetValue(s.Time, out var xi)) continue;
            var y = s.EntryHint ?? candles[xi].Close;
            if (s.Side == Side.Buy) buyPts.Add(new ObservablePoint(xi, y));
            else sellPts.Add(new ObservablePoint(xi, y));
        }

        if (buyPts.Count > 0)
        {
            Series.Add(new ScatterSeries<ObservablePoint>
            {
                Name = "BUY",
                Values = buyPts,
                GeometrySize = 10
            });
        }

        if (sellPts.Count > 0)
        {
            Series.Add(new ScatterSeries<ObservablePoint>
            {
                Name = "SELL",
                Values = sellPts,
                GeometrySize = 10
            });
        }

        // Zones as translucent rectangular sections.
        // We anchor by Created time to nearest 5m index.
        foreach (var z in res.Zones)
        {
            if (!indexOf.TryGetValue(z.Created, out var xi))
            {
                // if created on a coarser bar (e.g., M15 bucket), try find exact bucket start in base
                xi = candles.FindIndex(c => c.Time == z.Created);
                if (xi < 0) continue;
            }

            int xj = Math.Min(candles.Count - 1, xi + 60); // show for ~60 bars

            Sections.Add(new RectangularSection
            {
                Xi = xi,
                Xj = xj,
                Yi = z.Low,
                Yj = z.High,
                Fill = new SolidColorPaint(new SKColor(120, 120, 120, 30)),
                Stroke = new SolidColorPaint(new SKColor(120, 120, 120, 80))
            });
        }

        XAxes.Add(new Axis
        {
            Name = "Bars",
            MinLimit = 0,
            MaxLimit = candles.Count
        });

        YAxes.Add(new Axis
        {
            Name = "Price",
            MinLimit = candles.Min(c => c.Low) * 0.995,
            MaxLimit = candles.Max(c => c.High) * 1.005
        });
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}
