﻿using System;
using System.Windows.Controls;
using CrossUI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AlignmentX = System.Windows.Media.AlignmentX;
using AlignmentY = System.Windows.Media.AlignmentY;
using Brush = System.Windows.Media.Brush;
using BrushMappingMode = System.Windows.Media.BrushMappingMode;
using Color = Avalonia.Media.Color;
using Drawing = System.Windows.Media.Drawing;
using DrawingBrush = System.Windows.Media.DrawingBrush;
using DrawingCollection = System.Windows.Media.DrawingCollection;
using DrawingContext = System.Windows.Media.DrawingContext;
using DrawingGroup = System.Windows.Media.DrawingGroup;
using DrawingImage = System.Windows.Media.DrawingImage;
using Geometry = System.Windows.Media.Geometry;
using GeometryDrawing = System.Windows.Media.GeometryDrawing;
using MatrixTransform = System.Windows.Media.MatrixTransform;
using Pen = System.Windows.Media.Pen;
using RectangleGeometry = System.Windows.Media.RectangleGeometry;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Stretch = System.Windows.Media.Stretch;
using TileBrush = System.Windows.Media.TileBrush;
using TileMode = System.Windows.Media.TileMode;
using Transform = System.Windows.Media.Transform;
using WPoint = System.Windows.Point;
using WSize = System.Windows.Size;
using WRect = System.Windows.Rect;
using WColor = System.Windows.Media.Color;
using WMatrix = System.Windows.Media.Matrix;
namespace Avalonia.RenderTests.WpfCompare;

internal static class WpfConvertExtensions
{
    public static WPoint ToWpf(this Point pt) => new(pt.X, pt.Y);
    public static WSize ToWpf(this Size size) => new(size.Width, size.Height);
    public static WRect ToWpf(this Rect rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);
    public static WColor ToWpf(this Color color) => WColor.FromArgb(color.A, color.R, color.G, color.B);
    public static WMatrix ToWpf(this Matrix m) => new WMatrix(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);
}

internal class WpfCrossControl : Panel
{
    private readonly CrossControl _src;
    private readonly Dictionary<CrossControl, WpfCrossControl> _children;

    public WpfCrossControl(CrossControl src)
    {
        _src = src;
        _children = src.Children.ToDictionary(x => x, x => new WpfCrossControl(x));
        Width = src.Bounds.Width;
        Height = src.Bounds.Height;
        RenderTransform = new MatrixTransform(src.RenderTransform.ToWpf());
        foreach (var ch in src.Children)
        {
            var c = _children[ch];
            this.Children.Add(c);
        }
    }
    
    protected override WSize MeasureOverride(WSize availableSize)
    {
        foreach (var ch in _children)
            ch.Value.Measure(ch.Key.Bounds.Size.ToWpf());
        return _src.Bounds.Size.ToWpf();
    }

    protected override WSize ArrangeOverride(WSize finalSize)
    {
        foreach (var ch in _children)
            ch.Value.Arrange(ch.Key.Bounds.ToWpf());
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnRender(DrawingContext context)
    {
        _src.Render(new WpfCrossDrawingContext(context));
    }
}

internal class WpfCrossDrawingContext : ICrossDrawingContext
{
    private readonly DrawingContext _ctx;

    public WpfCrossDrawingContext(DrawingContext ctx)
    {
        _ctx = ctx;
    }

    private static Transform? ConvertTransform(Matrix? m) => m == null ? null : new MatrixTransform(m.Value.ToWpf());

    private static Geometry ConvertGeometry(CrossGeometry g)
    {
        if (g is CrossRectangleGeometry rg)
            return new RectangleGeometry(rg.Rect.ToWpf());
        else if (g is CrossSvgGeometry svg)
            return Geometry.Parse(svg.Path);
        else if (g is CrossEllipseGeometry ellipse)
            return new EllipseGeometry(ellipse.Rect.ToWpf());
        throw new NotSupportedException();
    }

    private static Drawing ConvertDrawing(CrossDrawing src)
    {
        if (src is CrossDrawingGroup g)
            return new DrawingGroup() { Children = new DrawingCollection(g.Children.Select(ConvertDrawing)) };
        if (src is CrossGeometryDrawing geo)
            return new GeometryDrawing()
            {
                Geometry = ConvertGeometry(geo.Geometry), Brush = ConvertBrush(geo.Brush), Pen = ConvertPen(geo.Pen)
            };
        throw new NotSupportedException();
    }

    private static Brush? ConvertBrush(CrossBrush? brush)
    {
        if (brush == null)
            return null;
        static Brush Sync(Brush dst, CrossBrush src)
        {
            dst.Opacity = src.Opacity;
            dst.Transform = ConvertTransform(src.Transform);
            dst.RelativeTransform = ConvertTransform(src.RelativeTransform);
            return dst;
        }

        static Brush SyncTile(TileBrush dst, CrossTileBrush src)
        {
            dst.Stretch = (Stretch)src.Stretch;
            dst.AlignmentX = (AlignmentX)src.AlignmentX;
            dst.AlignmentY = (AlignmentY)src.AlignmentY;
            dst.TileMode = (TileMode)src.TileMode;
            dst.Viewbox = src.Viewbox.ToWpf();
            dst.ViewboxUnits = (BrushMappingMode)src.ViewboxUnits;
            dst.Viewport = src.Viewport.ToWpf();
            dst.ViewportUnits = (BrushMappingMode)src.ViewportUnits;
            return Sync(dst, src);
        }

        static Brush SyncGradient(GradientBrush dst, CrossGradientBrush src)
        {
            dst.MappingMode = (BrushMappingMode)src.MappingMode;
            dst.SpreadMethod = (GradientSpreadMethod)src.SpreadMethod;
            dst.GradientStops =
                new GradientStopCollection(src.GradientStops.Select(s => new GradientStop(s.Color.ToWpf(), s.Offset)));
            return Sync(dst, src);
        }

        if (brush is CrossSolidColorBrush br)
            return Sync(new SolidColorBrush(br.Color.ToWpf()), brush);
        if (brush is CrossDrawingBrush db)
            return SyncTile(new DrawingBrush(ConvertDrawing(db.Drawing)), db);
        if (brush is CrossRadialGradientBrush radial)
            return SyncGradient(new RadialGradientBrush()
            {
                RadiusX = radial.RadiusX,
                RadiusY = radial.RadiusY,
                Center = radial.Center.ToWpf(),
                GradientOrigin = radial.GradientOrigin.ToWpf()
            }, radial);
        throw new NotSupportedException();
    }

    private static Pen? ConvertPen(CrossPen? pen)
    {
        if (pen == null)
            return null;
        return new Pen(ConvertBrush(pen.Brush), pen.Thickness);
    }

    private static ImageSource ConvertImage(CrossImage image)
    {
        if (image is CrossBitmapImage bi)
            return new BitmapImage(new Uri(bi.Path, UriKind.Absolute));
        if (image is CrossDrawingImage di)
            return new DrawingImage(ConvertDrawing(di.Drawing));
        throw new NotSupportedException();
    }
    
    public void DrawRectangle(CrossBrush? brush, CrossPen? pen, Rect rc) => _ctx.DrawRectangle(ConvertBrush(brush), ConvertPen(pen), rc.ToWpf());
    public void DrawGeometry(CrossBrush? brush, CrossPen? pen, CrossGeometry geo) => 
        _ctx.DrawGeometry(ConvertBrush(brush), ConvertPen(pen), ConvertGeometry(geo));

    public void DrawImage(CrossImage image, Rect rc) => _ctx.DrawImage(ConvertImage(image), rc.ToWpf());
}
