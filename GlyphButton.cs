using System.ComponentModel;

namespace DehumidifierControl;

using static Graphics;

/// <summary>Кнопка со значкомъ, нарисованнымъ въ событіи PaintImage (Paint у Control уже занято — онъ рисуетъ саму кнопку).
/// Обработчикъ получаетъ чистую квадратную картинку въ высоту шрифта кнопки; она становится Image, прежняя освобождается.
/// Перерисовывается сама, когда смѣнились шрифтъ (въ томъ числѣ подъ DPI другого экрана), цвѣтъ текста или системные цвѣта.</summary>
[DefaultEvent(nameof(PaintImage))]
sealed class GlyphButton : Button
{
	PaintEventHandler? paintImage;
	public event PaintEventHandler? PaintImage
	{
		add    { paintImage += value; InvalidateImage(); }
		remove { paintImage -= value; InvalidateImage(); }
	}

	/// <summary>Перерисовать значокъ: PaintImage на свѣжей прозрачной картинкѣ — и въ Image.</summary>
	public void InvalidateImage()
	{
		int size = Font.Height;
		Bitmap bitmap = new(size, size);
		using (Graphics g = FromImage(bitmap))
		using (PaintEventArgs e = new(g, new(default, new Size(size, size))))
			paintImage?.Invoke(this, e);
		Image? old = Image;
		Image = bitmap;
		old?.Dispose();
	}

	protected override void OnFontChanged(EventArgs e)
	{
		base.OnFontChanged(e);
		InvalidateImage();
	}

	protected override void OnForeColorChanged(EventArgs e)
	{
		base.OnForeColorChanged(e);
		InvalidateImage();
	}

	/// <summary>Включили или выключили высокую контрастность — ControlText, а съ нимъ и значокъ, могъ смѣниться.</summary>
	protected override void OnSystemColorsChanged(EventArgs e)
	{
		base.OnSystemColorsChanged(e);
		InvalidateImage();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing) Image?.Dispose();
		base.Dispose(disposing);
	}
}
