using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Consts
{
    public enum AxisDir { Positive, Negative }

    public sealed class ViewGrid
    {
        public int Columns { get; }
        public int Rows { get; }

        public int CellWidth { get; }
        public int CellHeight { get; }

        public int GutterX { get; }
        public int GutterY { get; }

        // teraz set; żeby dało się przestawiać
        public int OriginX { get; private set; }
        public int OriginY { get; private set; }

        // opcjonalnie kierunki osi
        public AxisDir ColDir { get; }
        public AxisDir RowDir { get; }

        public ViewGrid(
            int columns, int rows,
            int cellWidth, int cellHeight,
            int originX = 0, int originY = 0,
            int gutterX = 0, int gutterY = 0,
            AxisDir colDir = AxisDir.Positive,
            AxisDir rowDir = AxisDir.Positive)
        {
            Columns = columns; Rows = rows;
            CellWidth = cellWidth; CellHeight = cellHeight;
            GutterX = gutterX; GutterY = gutterY;
            OriginX = originX; OriginY = originY;
            ColDir = colDir; RowDir = rowDir;
        }

        public void SetOrigin(int x, int y) { OriginX = x; OriginY = y; }

        public (int x, int y) GetCellOrigin(int col, int row)
        {
            var stepX = (CellWidth + GutterX) * (ColDir == AxisDir.Positive ? 1 : -1);
            var stepY = (CellHeight + GutterY) * (RowDir == AxisDir.Positive ? 1 : -1);
            return (OriginX + col * stepX, OriginY + row * stepY);
        }

        /// <summary>
        /// Ustawia origin tak, aby lewy-dolny róg komórki (col,row) był w punkcie (worldX,worldY).
        /// </summary>
        public void AlignCellToPoint(int col, int row, int worldX = 0, int worldY = 0)
        {
            var stepX = (CellWidth + GutterX) * (ColDir == AxisDir.Positive ? 1 : -1);
            var stepY = (CellHeight + GutterY) * (RowDir == AxisDir.Positive ? 1 : -1);
            OriginX = worldX - col * stepX;
            OriginY = worldY - row * stepY;
        }
    }

    public sealed class ViewGridLayout
    {
        private readonly ViewGrid _grid;

        // Przypisania widok -> (col,row, dx, dy)
        private readonly Dictionary<string, (int col, int row, int dx, int dy)> _cellMap = new();

        public ViewGridLayout(ViewGrid grid) => _grid = grid;

        public ViewGridLayout At(string viewName, int col, int row, int dx = 0, int dy = 0)
        {
            _cellMap[viewName] = (col, row, dx, dy);
            return this;
        }

        public bool TryGetCell(string viewName, out (int col, int row, int dx, int dy) pos)
            => _cellMap.TryGetValue(viewName, out pos);

        /// <summary>
        /// Zastosuj layout: ustawia XOffset/YOffset widoków tak,
        /// by lewy-dolny róg każdego widoku pokrył się z lewym-dolnym rogiem komórki + ewent. lokalny offset.
        /// </summary>
        public void ApplyTo(ViewsList views)
        {
            foreach (var kv in _cellMap)
            {
                var name = kv.Key;
                var (col, row, dx, dy) = kv.Value;
                var (cx, cy) = _grid.GetCellOrigin(col, row);
                views.SetView(name, cx + dx, cy + dy);
            }
        }
    }

    public static class GridPresets
    {
        // mapa NORM -> nazwa widoku -> (col,row)
        public static readonly Dictionary<Norm, Dictionary<string, (int col, int row)>> Cells = new()
        {
            [Norm.ISO] = new()
            {
                [ViewName.RightFront] = (0, 5),
                [ViewName.Operational] = (1, 5),
                [ViewName.LeftFront] = (2, 5),
                [ViewName.Back] = (3, 5),
                [ViewName.Down] = (1, 4),
                [ViewName.DownUp] = (2, 4),
                [ViewName.Up] = (1, 6),
                [ViewName.UpUp] = (1, 7),
                [ViewName.Frame] = (1, 3),
                [ViewName.Roof] = (1, 2),
            },
            [Norm.US] = new()
            {
                [ViewName.RightFront] = (2, 5),
                [ViewName.Operational] = (1, 5),
                [ViewName.LeftFront] = (0, 5),
                [ViewName.Back] = (0, 4),
                [ViewName.Down] = (1, 4),
                [ViewName.DownUp] = (2, 4),
                [ViewName.Up] = (1, 6),
                [ViewName.UpUp] = (1, 7),
                [ViewName.Frame] = (1, 3),
                [ViewName.Roof] = (1, 2),
            },
            [Norm.PROD] = new()
            {
                [ViewName.RightFront] = (2, 5),
                [ViewName.Operational] = (1, 5),
                [ViewName.LeftFront] = (0, 5),
                [ViewName.Back] = (0, 4),
                [ViewName.Down] = (1, 4),
                [ViewName.DownUp] = (2, 4),
                [ViewName.Up] = (1, 6),
                [ViewName.UpUp] = (1, 7),
                [ViewName.Frame] = (1, 3),
                [ViewName.Roof] = (1, 2),
            }
        };
    }
}
