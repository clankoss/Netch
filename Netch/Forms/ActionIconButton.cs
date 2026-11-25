using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Netch.Forms
{
    internal enum ActionGlyph { Edit, Delete, Latency, Copy }

    internal sealed class ActionIconButton : Button
    {
        private bool _hovered;
        private bool _pressed;

        [DefaultValue(ActionGlyph.Edit)]
        public ActionGlyph Glyph { get; set; }

        public ActionIconButton()
        {
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(28, 26);
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _pressed = e.Button == MouseButtons.Left;
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(System.EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
            var scale = DeviceDpi / 96f;
            var size = System.Math.Min(Width, Height) / scale - 2f;
            if (size <= 0)
                return;

            var state = graphics.Save();
            graphics.TranslateTransform((Width - size * scale) / 2f, (Height - size * scale) / 2f);
            graphics.ScaleTransform(scale, scale);

            var bounds = new RectangleF(0, 0, size, size);
            if (Enabled && (_hovered || Focused))
            {
                using var fill = new SolidBrush(Glyph == ActionGlyph.Delete
                    ? _pressed ? Color.FromArgb(252, 205, 210) : Color.FromArgb(255, 232, 235)
                    : _pressed ? Color.FromArgb(198, 219, 248) : Color.FromArgb(226, 237, 252));
                using var path = new GraphicsPath();
                var diameter = System.Math.Min(10f, size);
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                graphics.FillPath(fill, path);
            }

            var iconScale = System.Math.Min(size, 16f) / 16f;
            graphics.TranslateTransform((size - 16 * iconScale) / 2f, (size - 16 * iconScale) / 2f);
            graphics.ScaleTransform(iconScale, iconScale);
            var color = !Enabled ? SystemColors.GrayText :
                Glyph == ActionGlyph.Delete && _hovered ? Color.FromArgb(181, 47, 58) : SystemColors.ControlText;
            using var pen = new Pen(color, 1.45f)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
            };

            switch (Glyph)
            {
                case ActionGlyph.Edit:
                    graphics.DrawPolygon(pen, new[] { new PointF(2.5f, 13.5f), new PointF(3.3f, 9.9f),
                        new PointF(11.2f, 2f), new PointF(14f, 4.8f), new PointF(6.1f, 12.7f) });
                    graphics.DrawLine(pen, 9.8f, 3.4f, 12.6f, 6.2f);
                    break;
                case ActionGlyph.Delete:
                    graphics.DrawLine(pen, 2.5f, 4f, 13.5f, 4f);
                    graphics.DrawLines(pen, new[] { new PointF(6, 4), new PointF(6, 2), new PointF(10, 2), new PointF(10, 4) });
                    graphics.DrawLines(pen, new[] { new PointF(4, 4), new PointF(4.8f, 14), new PointF(11.2f, 14), new PointF(12, 4) });
                    graphics.DrawLine(pen, 8, 6.5f, 8, 11.5f);
                    break;
                case ActionGlyph.Latency:
                    graphics.DrawLines(pen, new[] { new PointF(1, 8), new PointF(4.2f, 8), new PointF(6.6f, 3),
                        new PointF(9.2f, 13), new PointF(11.6f, 8), new PointF(15, 8) });
                    break;
                case ActionGlyph.Copy:
                    graphics.DrawRectangle(pen, 5, 5, 8.5f, 9);
                    graphics.DrawLines(pen, new[] { new PointF(3, 11.5f), new PointF(2.5f, 11.5f),
                        new PointF(2.5f, 2.5f), new PointF(11, 2.5f), new PointF(11, 3) });
                    break;
            }

            graphics.Restore(state);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
        }
    }
}
