using Klimor.WebApi.DXF.Consts;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using static netDxf.Entities.HatchBoundaryPath;
using Line = netDxf.Entities.Line;

namespace Klimor.WebApi.DXF.Services
{
    public class GridDrawer
    {
        private readonly DxfDocument _doc;
        private readonly Layer _layer;

        public double TextHeight { get; set; } = 20;
        public double CellLabelOffsetX { get; set; } = 150;
        public double CellLabelOffsetY { get; set; } = 150;

        // UWAGA: jeżeli u Ciebie warstwy robi się inaczej, możesz pominąć cały fragment z Layer.
        public GridDrawer(DxfDocument document, string layerName = "GRID")
        {
            _doc = document;
            _layer = _doc.Layers.FirstOrDefault(l => l.Name == layerName)
                     ?? new Layer(layerName) { Color = AciColor.LightGray, Linetype = Linetype.DashDot };
            if (!_doc.Layers.Contains(_layer))
                _doc.Layers.Add(_layer);
        }

        public void Draw(ViewGrid grid)
        {
            var stepX = grid.CellWidth + grid.GutterX;
            var stepY = grid.CellHeight + grid.GutterY;

            var totalWidth = grid.Columns * grid.CellWidth + (grid.Columns - 1) * grid.GutterX;
            var totalHeight = grid.Rows * grid.CellHeight + (grid.Rows - 1) * grid.GutterY;

            double x0 = grid.OriginX;
            double y0 = grid.OriginY;
            double x1 = x0 + totalWidth;
            double y1 = y0 + totalHeight;

            // pionowe
            for (int c = 0; c <= grid.Columns; c++)
            {
                double x = x0 + c * stepX;
                AddLine(x, y0, x, y1);
            }

            // poziome
            for (int r = 0; r <= grid.Rows; r++)
            {
                double y = y0 + r * stepY;
                AddLine(x0, y, x1, y);
            }

            // ramka z 4 linii (zamiast LwPolyline)
            AddLine(x0, y0, x1, y0);
            AddLine(x1, y0, x1, y1);
            AddLine(x1, y1, x0, y1);
            AddLine(x0, y1, x0, y0);

            // etykiety kolumn (na dole)
            for (int c = 0; c < grid.Columns; c++)
            {
                double cx = x0 + c * stepX + grid.CellWidth / 2.0;
                AddText($"C({c})", cx, y0 - TextHeight * 0.8);
            }

            // etykiety wierszy (po lewej)
            for (int r = 0; r < grid.Rows; r++)
            {
                double ry = y0 + r * stepY + grid.CellHeight / 2.0;
                AddText($"R({r})", x0 - TextHeight * 0.8, ry);
            }

            // etykiety w komórkach (lewodolny róg komórki)
            for (int c = 0; c < grid.Columns; c++)
            {
                for (int r = 0; r < grid.Rows; r++)
                {
                    var (ox, oy) = grid.GetCellOrigin(c, r);
                    AddText($"C({c}) R({r})",
                            ox + CellLabelOffsetX, oy + CellLabelOffsetY);
                }
            }
        }

        private void AddLine(double x0, double y0, double x1, double y1)
        {
            var line = new Line(new Vector2(x0, y0), new Vector2(x1, y1));
            if (_layer is not null) line.Layer = _layer;
            _doc.Entities.Add(line);
        }

        private void AddText(string text, double x, double y)
        {
            var t = new Text(text, new Vector3(x, y, 0), TextHeight);
            if (_layer is not null) t.Layer = _layer;
            _doc.Entities.Add(t);
        }
    }

}
