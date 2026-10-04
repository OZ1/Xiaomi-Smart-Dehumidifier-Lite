namespace DehumidifierControl;

using static Math;

/// <summary>Влажный воздухъ: сколько воды въ немъ при данныхъ температурѣ и относительной влажности и обратно.
/// Давленіе насыщеннаго пара — формула Магнуса съ коэффиціентами Алдучова и Эскриджа (1996, ихъ же совѣтуетъ WMO):
/// отъ −40 до +50 °Ц ошибка меньше 0,4 %.</summary>
public static class Psychrometrics
{
	const double A = 6.1094; // гПа
	const double B = 17.625;
	const double C = 243.04; // °Ц

	/// <summary>Газовая постоянная водяного пара, дѣлённая на 100 (гПа → Па) и на 1000 (кг → г): ρ = e / (R·T) → 216,7·e/T г/м³.</summary>
	const double VaporFactor = 216.7;

	const double Kelvin = 273.15;

	/// <summary>Давленіе насыщеннаго пара надъ водою, гПа.</summary>
	public static double SaturationPressure(double t) => A * Exp(B * t / (t + C));

	/// <summary>Абсолютная влажность — граммовъ воды въ кубометрѣ воздуха.</summary>
	public static double AbsoluteHumidity(double t, double rh) => VaporFactor * (rh / 100 * SaturationPressure(t)) / (t + Kelvin);

	/// <summary>Относительная влажность, %, при которой въ кубометрѣ ρ граммовъ воды.</summary>
	public static double RelativeHumidity(double t, double rho) => 100 * rho * (t + Kelvin) / VaporFactor / SaturationPressure(t);

	/// <summary>Точка росы, °Ц: до какой температуры остудить воздухъ, чтобы пошёлъ конденсатъ.</summary>
	public static double DewPoint(double t, double rh)
	{
		double g = Log(Max(rh, 0.01) / 100) + B * t / (t + C);
		return C * g / (B - g);
	}

	/// <summary>Влагосодержаніе, граммовъ воды на килограммъ сухого воздуха (давленіе по умолчанію — нормальное).</summary>
	public static double MixingRatio(double t, double rh, double pressure = 1013.25)
	{
		double e = rh / 100 * SaturationPressure(t);
		return 621.98 * e / (pressure - e);
	}

	/// <summary>Сколько граммовъ воды убрать изъ воздуха комнаты объёмомъ volume м³, чтобы при той же температурѣ влажность стала target.</summary>
	public static double WaterToRemove(double volume, double t, double rh, double target) => volume * (AbsoluteHumidity(t, rh) - AbsoluteHumidity(t, target));
}
