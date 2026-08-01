using System;
using System.Drawing;
using System.Windows.Forms;

class CustomScrollPanel : Panel
{
    bool _dragging;
    int _dragStartY;
    int _dragStartOffset;
    int _offset;

    const int TrackW = 6;
    const int TrackPad = 4;
    public const int StripW = TrackW + 8;

    public CustomScrollPanel()
    {
        AutoScroll = false;
        BackColor = Color.FromArgb(32, 32, 32);
        TabStop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    Control VisibleChild()
    {
        foreach (Control c in Controls)
            if (c.Visible) return c;
        return null;
    }

    int ViewportH { get { return ClientSize.Height; } }
    int ContentH
    {
        get
        {
            Control c = VisibleChild();
            return c != null ? c.Height : 0;
        }
    }
    int MaxScroll { get { return Math.Max(0, ContentH - ViewportH); } }
    bool NeedScroll { get { return MaxScroll > 0; } }

    void ClampOffset()
    {
        int max = MaxScroll;
        if (_offset > max) _offset = max;
        if (_offset < 0) _offset = 0;
    }

    void PositionChild()
    {
        Control c = VisibleChild();
        if (c != null) c.Top = -_offset;
    }

    public void RefreshLayout()
    {
        ClampOffset();
        PositionChild();
        Invalidate();
    }

    public void ScrollTop()
    {
        _offset = 0;
        PositionChild();
        Invalidate();
    }

    Rectangle ThumbRect()
    {
        if (!NeedScroll) return new Rectangle(0, 0, 0, 0);
        int trackH = ViewportH - TrackPad * 2;
        int thumbH = Math.Max(28, (int)((float)trackH * ViewportH / Math.Max(1, ContentH)));
        int travel = trackH - thumbH;
        int thumbTop = TrackPad + (int)((float)_offset * travel / Math.Max(1, MaxScroll));
        return new Rectangle(ClientSize.Width - TrackW - 4, thumbTop, TrackW, thumbH);
    }

    void ScrollTo(int value)
    {
        _offset = value;
        ClampOffset();
        PositionChild();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!NeedScroll) return;
        int step = 48;
        int target = e.Delta > 0 ? _offset - step : _offset + step;
        target = Math.Max(0, Math.Min(MaxScroll, target));
        ScrollTo(target);
        base.OnMouseWheel(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RefreshLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (!NeedScroll) return;
        int trackX = ClientSize.Width - TrackW - 4;
        using (var pen = new Pen(Color.FromArgb(45, 45, 45)))
            e.Graphics.DrawLine(pen, trackX + TrackW / 2, TrackPad,
                trackX + TrackW / 2, ViewportH - TrackPad);
        Rectangle r = ThumbRect();
        using (var brush = new SolidBrush(Color.FromArgb(85, 85, 85)))
            e.Graphics.FillRectangle(brush, r);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (!NeedScroll) return;
        if (e.X < ClientSize.Width - TrackW - 12) return;
        Rectangle thumb = ThumbRect();
        if (e.Y >= thumb.Top && e.Y <= thumb.Bottom)
        {
            _dragging = true;
            _dragStartY = e.Y;
            _dragStartOffset = _offset;
        }
        else
        {
            int target = e.Y < thumb.Top ? _offset - ViewportH / 2 : _offset + ViewportH / 2;
            target = Math.Max(0, Math.Min(MaxScroll, target));
            ScrollTo(target);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        int trackH = ViewportH - TrackPad * 2;
        int thumbH = Math.Max(28, (int)((float)trackH * ViewportH / Math.Max(1, ContentH)));
        int travel = trackH - thumbH;
        int deltaY = e.Y - _dragStartY;
        int target = _dragStartOffset + (int)((float)deltaY * MaxScroll / Math.Max(1, travel));
        target = Math.Max(0, Math.Min(MaxScroll, target));
        ScrollTo(target);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }
}
