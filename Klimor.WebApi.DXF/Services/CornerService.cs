using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Services
{
    public enum AnchorPos
    {
        BottomLeft,
        BottomRight,
        TopRight,
        TopLeft
    }

    /// <summary>
    /// Pomocnik do rysowania zamalowanych narożników (kwadratów) w DXF.
    /// Wersja instancyjna – brak metod statycznych.
    /// </summary>
    public class CornerService
    {
        private readonly DxfDocument _dxf;
        private readonly Layer _layer;

        // domyślne ustawienia dla instancji
        private readonly double _defaultSize;
        private readonly short _defaultColorIndex;
        private readonly AnchorPos _defaultAnchor;

        public CornerService(DxfDocument dxf, Layer layer,
                            double defaultSize = 50.0,
                            AnchorPos defaultAnchor = AnchorPos.BottomLeft,
                            short defaultColorIndex = 7)
        {
            _dxf = dxf ?? throw new ArgumentNullException(nameof(dxf));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _defaultSize = defaultSize;
            _defaultAnchor = defaultAnchor;
            _defaultColorIndex = defaultColorIndex;
        }

        /// <summary>
        /// Rysuje zamalowany kwadrat (narożnik) w zadanym punkcie.
        /// </summary>
        /// <param name="x">Współrzędna X punktu odniesienia (zależna od kotwicy).</param>
        /// <param name="y">Współrzędna Y punktu odniesienia (zależna od kotwicy).</param>
        /// <param name="size">Rozmiar kwadratu; jeśli null, użyje domyślnego z konstruktora.</param>
        /// <param name="anchor">Kotwica; jeśli null, użyje domyślnej z konstruktora.</param>
        /// <param name="colorIndex">ACI kolor wypełnienia/obrysu; jeśli null, użyje domyślnego.</param>
        public void AddFilledCorner(double x, double y,
                                    double size = 50.0,
                                    AnchorPos anchor = AnchorPos.BottomLeft,
                                    short aciColor = 9)
        {
            // wyznacz lewy-dolny narożnik kwadratu w zależności od kotwicy
            double x0 = x, y0 = y;
            switch (anchor)
            {
                case AnchorPos.BottomLeft: x0 = x; y0 = y; break;
                case AnchorPos.BottomRight: x0 = x - size; y0 = y; break;
                case AnchorPos.TopRight: x0 = x - size; y0 = y - size; break;
                case AnchorPos.TopLeft: x0 = x; y0 = y - size; break;
            }

            // wierzchołki kwadratu (zgodnie z Twoim stylem)
            var cornerVertices = new List<Polyline2DVertex>
            {
                new Polyline2DVertex(x0,         y0,          0),
                new Polyline2DVertex(x0 + size,  y0,          0),
                new Polyline2DVertex(x0 + size,  y0 + size,   0),
                new Polyline2DVertex(x0,         y0 + size,   0),
            };

            var cornerPoly = new Polyline2D(cornerVertices, true)
            {
                Layer = _layer
            };

            var hatch = new Hatch(HatchPattern.Solid, true)
            {
                Layer = _layer,
                Color = new AciColor(aciColor),
            };

            hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));

            // WAŻNE: tylko hatch trafia do dokumentu
            _dxf.Entities.Add(hatch);
        }
    }
}
