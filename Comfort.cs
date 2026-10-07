namespace DehumidifierControl;

using static Math;
using static Comfort;

/// <summary>Ступени влажности въ комнатѣ — отъ слишкомъ сухо до слишкомъ влажно.</summary>
public enum Comfort : byte { TooDry, Dry, Ideal, Normal, Humid, TooHumid }

/// <summary>Оцѣнка влажности по ступенямъ Comfort — общая для окна и командной строки; къ протоколу осушителя отношенія не имѣетъ.</summary>
static class ComfortScale
{
	/// <summary>Оцѣнка влажности въ комнатѣ по медицинскимъ рекомендаціямъ:
	/// 30…60 % — допустимо по ГОСТ 30494-2011;
	/// 40…60 % — норма (диаграмма Стерлинга, 1986), изъ нея
	/// 40…50 % — лучше всего (тамъ же и совѣтъ EPA, клиники Мэйо — не выше 50 %);
	/// ниже 30 % сохнутъ слизистыя;
	/// EPA совѣтуетъ держать ниже 60 % — съ 60 % уже влажно;
	/// съ 70 % растутъ плѣсень и клещи.
	/// Ступень — сколько началъ ComfortStarts влажность уже достигла.</summary>
	public static Comfort HumidityComfort(byte humidity)
	{
		int i = ComfortStarts.BinarySearch(humidity);
		return (Comfort)(i < 0 ? ~i : i + 1);
	}

	/// <summary>Первое значеніе каждой ступени послѣ TooDry.</summary>
	public static ReadOnlySpan<byte> ComfortStarts => [/*Dry*/30, /*Ideal*/40, /*Normal*/51, /*Humid*/60, /*TooHumid*/70];

	/// <summary>Цвѣтъ ступени Comfort въ окнѣ: и у влажности въ комнатѣ, и на полоскѣ подъ шкалой.</summary>
	public static Color ComfortColor(Comfort comfort) => comfort switch
	{
		Ideal        => Color.ForestGreen,
		Normal       => Color.Olive,
		Dry or Humid => Color.Chocolate,
		_            => Color.Firebrick
	};

	/// <summary>Цвѣта ступеней для капли въ треѣ — мягкіе и свѣтлые, не какъ у надписи въ окнѣ: нѣжно-голубой, мятно-салатовый, оранжевый, кирпичный.</summary>
	public static Color TrayComfortColor(Comfort comfort) => comfort switch
	{
		Ideal        => Color.LightSkyBlue,
		Normal       => Color.LightGreen,
		Dry or Humid => Color.Orange,
		_            => Color.FromArgb(0xC4, 0x4E, 0x34), // кирпичный
	};

	// ───── плавный цвѣтъ влажности: площадки и переходы въ OKLCH ─────

	/// <summary>Половина ширины перехода, %: внутри ступени цвѣтъ стоитъ, за 3 % до границы и 3 % послѣ — переливается въ сосѣдній.</summary>
	public const double HumidityBlendPercent = 3;

	/// <summary>Цвѣтъ влажности: на площадкѣ — цвѣтъ ступени, у границы — переходъ въ OKLCH по smoothstep (3t² − 2t³):
	/// у краёвъ перехода цвѣтъ мѣняется медленно, поэтому площадка переходитъ въ переливъ безъ излома.
	/// Граница ступеней — посерединѣ между послѣднимъ значеніемъ одной и первымъ слѣдующей (ComfortStarts − 0,5);
	/// ступени шире двухъ переходовъ, поэтому влажность бываетъ у одной границы самое большее.</summary>
	public static Color HumidityColor(byte humidity) => HumidityColor(humidity, ComfortColor);

	/// <summary>То же съ другими цвѣтами ступеней — для капли въ треѣ.</summary>
	public static Color HumidityColor(byte humidity, Func<Comfort, Color> palette)
	{
		for (int i = 0; i < ComfortStarts.Length; i++)
		{
			double  d =  humidity - ComfortStarts[i] + 0.5; // разстояніе до границы между ступенями i и i + 1
			if (Abs(d) > HumidityBlendPercent) continue;
			double t = (d + HumidityBlendPercent) / (2 * HumidityBlendPercent);
			Color a = palette((Comfort) i);
			Color b = palette((Comfort)(i + 1));
			return OKLCH.Mix(a, b, (float)(t * t * (3 - 2 * t)));
		}
		return palette(HumidityComfort(humidity)); // далеко отъ всѣхъ границъ — на площадкѣ
	}
}
