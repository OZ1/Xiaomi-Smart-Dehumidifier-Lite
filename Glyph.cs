using System.Drawing.Drawing2D;

namespace DehumidifierControl;

using static Single;
using static Color;

/// <summary>Рисованные значки: что рисовать — здѣсь, когда и какимъ цвѣтомъ — рѣшаетъ окно.</summary>
static class Glyph
{
	/// <summary>Крупная капля во весь значокъ: остріе вверху, брюшко внизу; тонкій ободокъ — тёмный на свѣтлой панели задачъ, свѣтлый на тёмной:
	/// яркая капля на свѣтлой панели безъ него расплывается.</summary>
	public static void Drop(Graphics g, Rectangle bounds, Color color, bool lightTheme)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		float r = bounds.Width * 0.33f; // радіусъ брюшка: ширина капли — ⅔ высоты, вытянутая, не шарикъ
		PointF center = new(bounds.X + bounds.Width / 2f, bounds.Bottom - r - 0.75f);
		PointF tip    = new(bounds.X + bounds.Width / 2f, bounds.Top    +     0.75f);
		// Бока — вогнутыя дуги радіуса R: каждая проходитъ черезъ остріе и касается брюшка снаружи — безъ излома.
		// Центръ лѣваго бока — (tip.X − dx, tip.Y + dy): отъ острія на R, отъ центра брюшка на R + r (h — отъ острія до центра брюшка):
		// dx² + dy² = R², dx² + (h − dy)² = (R + r)² — вычитая, dy = (h² − 2Rr − r²) / 2h; правый бокъ — зеркально.
		// При R = (h² − r²) / 2r центръ бока на уровнѣ острія (dy = 0) и бокъ у острія отвѣсенъ — хвостикъ иглою;
		// меньше нельзя — дуги боковъ перехлестнутся; въ 1,6 раза больше — бока положе, хвостикъ сходится остріемъ.
		float h  = center.Y - tip.Y;
		float R  =      1.6f * (h * h - r * r) / (2 * r);
		float dy = (h * h - 2 * R * r - r * r) / (2 * h), dx = Sqrt(R * R - dy * dy);
		float toBelly = RadiansToDegrees(Atan2(h - dy, dx)); // отъ центра лѣваго бока къ центру брюшка — тамъ и точка касанія
		float toTip   = RadiansToDegrees(Atan2(  - dy, dx)); // отъ центра лѣваго бока къ острію
		using GraphicsPath drop = new();
		drop.AddArc(center.X - r,   center.Y - r,   2 * r, 2 * r,      -toBelly, 180 + 2 * toBelly); // брюшко по часовой: отъ правой точки касанія черезъ низъ до лѣвой
		drop.AddArc(tip.X - dx - R, tip.Y + dy - R, 2 * R, 2 * R,       toBelly, toTip - toBelly);   // лѣвый бокъ — вверхъ къ острію
		drop.AddArc(tip.X + dx - R, tip.Y + dy - R, 2 * R, 2 * R, 180 - toTip,   toTip - toBelly);   // правый бокъ — отъ острія внизъ
		drop.CloseFigure();
		using SolidBrush brush = new(color);
		using Pen rim = new(FromArgb(lightTheme ? 110 : 140, lightTheme ? Black : White), 1);
		g.FillPath(brush, drop);
		g.DrawPath(rim, drop);
	}
}
