using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Klimor.WebApi.DXF.Services
{
    using netDxf;
    using netDxf.Entities;
    using netDxf.Tables;

    public static class ArrowService
    {
        public static ArrowEntities AddArrow(
            DxfDocument dxf,
            Vector2 anchor,
            ArrowDirection direction,
            string label,
            double arrowSize,
            double padding,
            AciColor outlineColor,
            Layer layer,
            bool filled = false,
            AciColor? fillColor = null,
            double lengthFactor = 2.4,
            double headFactor = 0.75,
            double textHeightFactor = 0.30,
            TextStyle? textStyle = null
        )
        {
            var ents = CreateArrowEntities(
                anchor, direction, label, arrowSize, padding,
                outlineColor, layer, filled, fillColor,
                lengthFactor, headFactor, textHeightFactor, textStyle
            );

            if (ents.Fill != null) dxf.Entities.Add(ents.Fill);
            dxf.Entities.Add(ents.Outline);
            dxf.Entities.Add(ents.Label);

            return ents;
        }

        //public static ArrowEntities CreateArrowEntities(
        //    Vector2 anchor,
        //    ArrowDirection direction,
        //    string label,
        //    double arrowSize,
        //    double padding,
        //    AciColor outlineColor,
        //    Layer layer,
        //    bool filled = false,
        //    AciColor? fillColor = null,
        //    double lengthFactor = 2.4,
        //    double headFactor = 0.75,
        //    double textHeightFactor = 0.30,
        //    TextStyle? textStyle = null
        //)
        //{
        //    // --- identyczna geometria jak wcześniej, tylko zamiast List<EntityObject> zwracamy obiekty ---
        //    double H = arrowSize;
        //    double L = lengthFactor * H;
        //    double headLen = Math.Min(headFactor * H, L * 0.9);
        //    double halfH = H / 2.0;

        //    double xLeft = -L / 2.0;
        //    double xRight = L / 2.0;
        //    double xHeadBase = xRight - headLen;

        //    var local = new List<Vector2>
        //{
        //    new Vector2(xLeft,     -halfH),
        //    new Vector2(xHeadBase, -halfH),
        //    new Vector2(xRight,     0),
        //    new Vector2(xHeadBase,  halfH),
        //    new Vector2(xLeft,      halfH),
        //};

        //    double angDeg = direction switch
        //    {
        //        ArrowDirection.Right => 0,
        //        ArrowDirection.Up => 90,
        //        ArrowDirection.Left => 180,
        //        ArrowDirection.Down => 270,
        //        _ => 0
        //    };

        //    var pts = local
        //        .Select(p => Rotate(p, angDeg))
        //        .Select(p => new Vector2(p.X + anchor.X, p.Y + anchor.Y))
        //        .ToList();

        //    var outlineVerts = pts.Select(p => new Polyline2DVertex(p.X, p.Y, 0)).ToList();

        //    var outline = new Polyline2D(outlineVerts, true)
        //    {
        //        Layer = layer,
        //        Color = outlineColor
        //    };

        //    Hatch? hatch = null;
        //    if (filled)
        //    {
        //        var fc = fillColor ?? outlineColor;
        //        hatch = new Hatch(HatchPattern.Solid, true)
        //        {
        //            Layer = layer,
        //            Color = fc
        //        };

        //        var boundaryPoly = new Polyline2D(outlineVerts, true) { Layer = layer };
        //        hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { boundaryPoly }));
        //    }

        //    double textHeight = Math.Max(1e-6, H * textHeightFactor);

        //    var localTextShift = new Vector2(-headLen * 0.20, 0);
        //    var shift = Rotate(localTextShift, angDeg);

        //    var text = new Text(label ?? "", new Vector3(anchor.X + shift.X, anchor.Y + shift.Y, 0), textHeight)
        //    {
        //        Layer = layer,
        //        Color = outlineColor,
        //        Alignment = TextAlignment.MiddleCenter
        //    };

        //    if (textStyle != null)
        //        text.Style = textStyle;

        //    return new ArrowEntities
        //    {
        //        Outline = outline,
        //        Label = text,
        //        Fill = hatch
        //    };
        //}

        public static ArrowEntities CreateArrowEntities(
    Vector2 anchor,
    ArrowDirection direction,
    string label,
    double arrowSize,
    double padding,
    AciColor outlineColor,
    Layer layer,
    bool filled = false,
    AciColor? fillColor = null,
    double lengthFactor = 0.1,
    double headFactor = 0.15,
    double textHeightFactor = 1.40,
    TextStyle? textStyle = null,
    double shaftFactor = 0.40   // <-- NOWE: wysokość trzonka jako % H (0.55–0.65 wygląda “ikonowo”)
)
        {
            double H = arrowSize;
            double L = lengthFactor * H/2;
            double headLen = Math.Min(headFactor * H, L * 0.3);

            double halfH = H / 2.0;

            // --- NOWE: węższy trzonek => 7 punktów
            double shaftH = Math.Max(1e-6, shaftFactor * H);
            shaftH = Math.Min(shaftH, H);     // nie większy niż H
            double halfS = shaftH / 2.0;

            double xLeft = -L / 2.0;
            double xRight = L / 2.0;
            double xHeadBase = xRight - headLen;

            // 7-punktowy kształt (Right): trzonek węższy, grot pełnej wysokości
            var local = new List<Vector2>
    {
        new Vector2(xLeft,     -halfS), // 0: lewy dół trzonka
        new Vector2(xHeadBase, -halfS), // 1: prawy dół trzonka
        new Vector2(xHeadBase, -halfH), // 2: dół podstawy grotu (stopień)
        new Vector2(xRight,     0),     // 3: czubek
        new Vector2(xHeadBase,  halfH), // 4: góra podstawy grotu (stopień)
        new Vector2(xHeadBase,  halfS), // 5: prawy góra trzonka
        new Vector2(xLeft,      halfS), // 6: lewy góra trzonka
    };

            double angDeg = direction switch
            {
                ArrowDirection.Right => 0,
                ArrowDirection.Up => 90,
                ArrowDirection.Left => 180,
                ArrowDirection.Down => 270,
                _ => 0
            };

            var pts = local
                .Select(p => Rotate(p, angDeg))
                .Select(p => new Vector2(p.X + anchor.X, p.Y + anchor.Y))
                .ToList();

            var outlineVerts = pts.Select(p => new Polyline2DVertex(p.X, p.Y, 0)).ToList();

            var outline = new Polyline2D(outlineVerts, true)
            {
                Layer = layer,
                Color = outlineColor
            };

            Hatch? hatch = null;
            if (filled)
            {
                var fc = fillColor ?? outlineColor;
                hatch = new Hatch(HatchPattern.Solid, true)
                {
                    Layer = layer,
                    Color = fc
                };

                var boundaryPoly = new Polyline2D(outlineVerts, true) { Layer = layer };
                hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { boundaryPoly }));
            }

            double textHeight = Math.Max(1e-6, (H * textHeightFactor) - H/10);

            var localTextShift = new Vector2(-headLen * 0.20, 0);
            var shift = Rotate(localTextShift, angDeg);

            var text = new Text(label ?? "", new Vector3(anchor.X + shift.X, anchor.Y + shift.Y, 0), textHeight)
            {
                Layer = layer,
                Color = filled ? new AciColor(7) : outlineColor,
                Alignment = TextAlignment.MiddleCenter
            };

            if (textStyle != null)
                text.Style = textStyle;

            return new ArrowEntities
            {
                Outline = outline,
                Label = text,
                Fill = hatch
            };
        }

        private static Vector2 Rotate(Vector2 p, double angleDeg)
        {
            double a = angleDeg * Math.PI / 180.0;
            double ca = Math.Cos(a);
            double sa = Math.Sin(a);
            return new Vector2(p.X * ca - p.Y * sa, p.X * sa + p.Y * ca);
        }
    }

    public sealed class ArrowEntities
    {
        public Polyline2D Outline { get; init; }
        public Text Label { get; init; }
        public Hatch? Fill { get; init; } // null jeśli filled=false
    }

}