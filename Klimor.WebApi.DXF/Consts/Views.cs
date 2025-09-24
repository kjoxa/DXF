using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Consts
{
    public static class ViewName
    {
        public const string Operational = "Operational";
        public const string Back = "Back";
        public const string Up = "Up";
        public const string Down = "Down";
        public const string LeftFront = "LeftFront";
        public const string RightFront = "RightFront";
        public const string Frame = "Frame";
        public const string Roof = "Roof";
    }

    public class ViewElement
    {
        public string Name { get; set; }

        public int XOffset { get; set; }

        public int YOffset { get; set; }

        public ViewElement(string name, int x, int y) =>
            (Name, XOffset, YOffset) = (name, x, y);
    }

    public class ViewsList
    {
        private string waterMarkText = "Klimor";

        public Norm? CurrentNorm { get; private set; }

        public double AhuLength { get; set; }

        public double AhuHeight { get; set; }

        public double AhuWidth { get; set; }

        private Dictionary<string, ViewElement> _views = new()
        {
            [ViewName.Operational] = new(ViewName.Operational, 0, 0),
            [ViewName.Back] = new(ViewName.Back, 0, 5000),
            [ViewName.Up] = new(ViewName.Up, 0, 10000),
            [ViewName.Down] = new(ViewName.Down, 0, 15000),
            [ViewName.LeftFront] = new(ViewName.LeftFront, 0, 20000),
            [ViewName.RightFront] = new(ViewName.RightFront, 0, 25000),
            [ViewName.Frame] = new(ViewName.Frame, 0, 50000),
            [ViewName.Roof] = new(ViewName.Roof, 0, -50000)
        };

        public ViewElement this[string name] => _views[name];

        public ViewElement Operational => _views[ViewName.Operational];

        public ViewElement Back => _views[ViewName.Back];

        public ViewElement Up => _views[ViewName.Up];

        public ViewElement Down => _views[ViewName.Down];

        public ViewElement LeftFront => _views[ViewName.LeftFront];

        public ViewElement RightFront => _views[ViewName.RightFront];

        public ViewElement Frame => _views[ViewName.Frame];

        public ViewElement Roof => _views[ViewName.Roof];

        public IEnumerable<ViewElement> All => _views.Values;

        public void SetView(string name, int x, int y)
        {
            if (_views.TryGetValue(name, out var view))
            {
                view.XOffset = x;
                view.YOffset = y;
            }
            else
            {
                throw new ArgumentException($"View '{name}' does not exist.", nameof(name));
            }
        }

        private static readonly Dictionary<Norm, Dictionary<string, (int x, int y)>> _presets =
        new()
        {
            [Norm.ISO] = new()
            {
                [ViewName.RightFront] = (-6000, 0),
                [ViewName.Operational] = (0, 0),
                [ViewName.LeftFront] = (13000, 0),
                [ViewName.Back] = (18000, 0),
                [ViewName.Down] = (0, 6000),
                [ViewName.Up] = (0, -6000),
                [ViewName.Frame] = (0, -12000),
                [ViewName.Roof] = (0, -17000),
            },
            [Norm.US] = new()
            {
                [ViewName.RightFront] = (13000, 0),
                [ViewName.Operational] = (0, 0),
                [ViewName.LeftFront] = (-5500, 0),
                [ViewName.Back] = (-18500, 0),
                [ViewName.Down] = (0, -6000),
                [ViewName.Up] = (0, 6000),
                [ViewName.Frame] = (0, -12000),
                [ViewName.Roof] = (0, -17000),
            },
            [Norm.PROD] = new()
            {
                [ViewName.RightFront] = (13000, 0),
                [ViewName.Operational] = (0, 0),
                [ViewName.LeftFront] = (-5500, 0),
                [ViewName.Back] = (-18500, 0),
                [ViewName.Down] = (0, -6000),
                [ViewName.Up] = (0, 6000),
                [ViewName.Frame] = (0, -12000),
                [ViewName.Roof] = (0, -17000),
            }
        };

        public void ApplyNormOnGrid(Norm norm, ViewGrid grid, int localDx = 0, int localDy = 0)
        {
            if (!GridPresets.Cells.TryGetValue(norm, out var cellMap))
                throw new ArgumentOutOfRangeException(nameof(norm), $"Unsupported norm: {norm}");

            var layout = new ViewGridLayout(grid);
            foreach (var (name, cell) in cellMap)
                layout.At(name, cell.col, cell.row, localDx, localDy);

            layout.ApplyTo(this);
            CurrentNorm = norm;            
        }

        public IEnumerable<ViewElement> Select(params string[] names) => names.Select(n => this[n]);

        public IEnumerable<ViewElement> Except(params string[] names) => _views.Where(kv => !names.Contains(kv.Key)).Select(kv => kv.Value);

        public void SetWaterMark(string text) => waterMarkText = text;
        public string GetWaterMark() => waterMarkText;

        public void ApplyNorm(Norm norm)
        {
            if (!_presets.TryGetValue(norm, out var map))
                throw new ArgumentOutOfRangeException(nameof(norm), $"Unsupported norm: {norm}");

            foreach (var (name, coords) in map)
                SetView(name, coords.x, coords.y);

            CurrentNorm = norm;
        }
    }

    public enum Norm
    {
        ISO,
        US,
        PROD
    }
}
