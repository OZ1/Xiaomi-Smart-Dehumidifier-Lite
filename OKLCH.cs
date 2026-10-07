using System.Numerics;

namespace DehumidifierControl;

using static Single;
using static Vector3;
using static Matrix4x4;
using static Color;

/// <summary>Цвѣтовое пространство OKLCH (Björn Ottosson, 2020) — для ровныхъ на глазъ переливовъ.</summary>
static class OKLCH
{
	/// <summary>Матрицы OKLab (Björn Ottosson, 2020): линейный sRGB → LMS и LMS въ кубическомъ корнѣ → Lab; обратныя — ихъ обращеніемъ.</summary>
	static readonly Matrix4x4 LinearToLMS = Matrix(0.4122214708f,  0.5363325363f,  0.0514459929f,
	/**/                                           0.2119034982f,  0.6806995451f,  0.1073969566f,
	/**/                                           0.0883024619f,  0.2817188376f,  0.6299787005f);
	static readonly Matrix4x4 LMSToLab    = Matrix(0.2104542553f,  0.7936177850f, -0.0040720468f,
	/**/                                           1.9779984951f, -2.4285922050f,  0.4505937099f,
	/**/                                           0.0259040371f,  0.7827717662f, -0.8086757660f);
	static readonly Matrix4x4 LabToLMS    = Inverse(LMSToLab);
	static readonly Matrix4x4 LMSToLinear = Inverse(LinearToLMS);

	/// <summary>Матрица 3×3, записанная строками, какъ въ статьѣ. Vector3.Transform умножаетъ вектор-строку на матрицу (v·M),
	/// а въ статьѣ — матрица на вектор-столбецъ (M·v), поэтому кладётся транспонированной.</summary>
	static Matrix4x4 Matrix(float m11, float m12, float m13,
	/**/                    float m21, float m22, float m23,
	/**/                    float m31, float m32, float m33) => new(
		m11, m21, m31, 0,
		m12, m22, m32, 0,
		m13, m23, m33, 0,
		0,   0,   0,   1);

	static Matrix4x4 Inverse(Matrix4x4 matrix) => Invert(matrix, out Matrix4x4 inverse) ? inverse : throw new ArgumentException(null, nameof(matrix));

	/// <summary>Смѣсь двухъ цвѣтовъ въ OKLCH: всѣ три составляющія — линейно, тонъ — по короткой дугѣ.
	/// Въ OKLCH равные шаги и на глазъ равны, поэтому переливъ ровный, безъ грязной середины, какъ въ RGB.</summary>
	public static Color Mix(Color a, Color b, float t)
	{
		Vector3 from = FromColor(a), to = FromColor(b);
		float dh = to.Z - from.Z;
		if (dh >  float.Pi) dh -= float.Tau;
		if (dh < -float.Pi) dh += float.Tau;
		return ToColor(Lerp(from, to with { Z = from.Z + dh }, t));
	}

	/// <summary>sRGB → OKLCH: (свѣтлота 0…1, насыщенность, тонъ въ радіанахъ).</summary>
	static Vector3 FromColor(Color color)
	{
		static float Linear(byte c)
		{
			float  v = c / 255f;
			return v <= 0.04045f ? v / 12.92f : Pow((v + 0.055f) / 1.055f, 2.4f);
		}
		Vector3 lms = Transform(new(Linear(color.R), Linear(color.G), Linear(color.B)), LinearToLMS);
		Vector3 lab = Transform(new(Cbrt(lms.X), Cbrt(lms.Y), Cbrt(lms.Z)), LMSToLab);
		return new(lab.X, new Vector2(lab.Y, lab.Z).Length(), Atan2(lab.Z, lab.Y));
	}

	/// <summary>OKLCH → sRGB; что вышло за охватъ sRGB — прижимается къ 0…255.</summary>
	static Color ToColor(Vector3 lch)
	{
		(float sin, float cos) = SinCos(lch.Z);
		Vector3 lms = Transform(new(lch.X, lch.Y * cos, lch.Y * sin), LabToLMS);
		Vector3 rgb = Transform(lms * lms * lms, LMSToLinear);
		static int Gamma(float c) => (int)Round(255 * Clamp(c <= 0.0031308f ? 12.92f * c : 1.055f * Pow(c, 1 / 2.4f) - 0.055f, 0, 1));
		return FromArgb(Gamma(rgb.X), Gamma(rgb.Y), Gamma(rgb.Z));
	}
}
