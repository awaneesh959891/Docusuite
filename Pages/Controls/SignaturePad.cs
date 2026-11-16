using MauiApp.Models;

namespace MauiApp.Pages.Controls;

public class SignaturePad : GraphicsView, IDrawable
{
    private readonly StrokeCollection _strokes;
    private readonly Color _ink;

    public SignaturePad(StrokeCollection strokes, Color ink)
    {
        _strokes = strokes;
        _ink = ink;
        Drawable = this;
        BackgroundColor = Colors.White;
        StartInteraction += (_, e) => Begin(e);
        DragInteraction += (_, e) => Extend(e);
        _strokes.Changed += Invalidate;
    }

    private void Begin(TouchEventArgs e)
    {
        if (e.Touches.Length > 0)
            _strokes.Begin((float)e.Touches[0].X, (float)e.Touches[0].Y);
    }

    private void Extend(TouchEventArgs e)
    {
        if (e.Touches.Length > 0)
            _strokes.Extend((float)e.Touches[0].X, (float)e.Touches[0].Y);
    }

    public void Detach() => _strokes.Changed -= Invalidate;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = _ink;
        canvas.StrokeSize = 4;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        foreach (var stroke in _strokes.Strokes)
        {
            if (stroke.Count == 1)
            {
                canvas.FillColor = _ink;
                canvas.FillCircle(stroke[0].X, stroke[0].Y, 2);
                continue;
            }
            var path = new PathF(stroke[0].X, stroke[0].Y);
            for (int i = 1; i < stroke.Count; i++)
                path.LineTo(stroke[i].X, stroke[i].Y);
            canvas.DrawPath(path);
        }
    }
}
