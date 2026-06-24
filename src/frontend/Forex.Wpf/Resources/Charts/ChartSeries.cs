namespace Forex.Wpf.Resources.Charts;

using System.Windows.Media;

public class ChartSeries
{
    public string Name { get; set; } = string.Empty;
    public Color Color { get; set; }
    public IReadOnlyList<double> Values { get; set; } = [];
    public bool Fill { get; set; }
    public bool Dim { get; set; }
}

public class ChartData
{
    public IReadOnlyList<string> Labels { get; set; } = [];
    public IReadOnlyList<ChartSeries> Series { get; set; } = [];
}
