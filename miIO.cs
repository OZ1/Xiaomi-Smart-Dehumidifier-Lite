#pragma warning disable IDE1006 // Нарушение правила именования: Эти слова должны начинаться с прописных символов: siid
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DehumidifierControl;

using static Properties.Resources;

using static MD5;
using static String;
using static Convert;
using static JsonNode;
using static IPAddress;
using static Stopwatch;
using static Enumerable;
using static PaddingMode;
using static JsonSerializer;
using static BinaryPrimitives;
using static CancellationTokenSource;

public sealed class miIOException(string message) : Exception(message);
public sealed class miIO : IDisposable
{
	static readonly byte[] Hello = [0x21,0x31, 0,32, .. Repeat<byte>(0xFF, 28)];

	readonly string IP;
	readonly byte[] Token;           // 16 байт
	readonly byte[] IV;              // 16 байт
	readonly Aes AES = Aes.Create(); // 16 байт
	readonly UdpClient Udp = new();
	readonly SemaphoreSlim Lock = new(1, 1);

	uint DeviceId;
	uint Stamp;
	long StampAt;
	bool Handshaken;
	int MessageId = Random.Shared.Next(1, 9000);

	public TimeSpan Timeout { get; set; } = new(0, 0, seconds: 3);

	public miIO(string ip, ReadOnlySpan<char> tokenHex)
	{
		IP = ip;
		Token = FromHexString(tokenHex);
		if (Token.Length != 16)
			throw new ArgumentException(TokenFormat, nameof(tokenHex));
		byte[]     aesKey = HashData(Token);
		AES.Key =  aesKey;
		Span<byte>    keyToken = stackalloc byte[32];
		aesKey.CopyTo(keyToken);
		Token .CopyTo(keyToken[16..]);
		IV = HashData(keyToken);
		Udp.Connect(Parse(ip), 54321);
	}

	public async Task<JsonNode?> SendAsync(string method, JsonNode? parameters = null, CancellationToken ct = default)
	{
		await Lock.WaitAsync(ct);
		try
		{
			bool answered = Handshaken; // отвѣчало ли устройство на hello — чтобы вѣрно назвать причину неудачи
			for (int attempt = 0; attempt < 3; attempt++)
			{
				if (!Handshaken || attempt > 0)
				{
					if (!await HandshakeAsync(ct)) continue; // осушитель иногда пропускаетъ hello — это одна попытка, а не конецъ
					answered = true;
				}

				int id = ++MessageId;
				JsonObject request = new()
				{
					["id"] = id,
					["method"] = method,
					["params"] = parameters?.DeepClone() ?? new JsonArray(),
				};
				await Udp.SendAsync(BuildPacket(SerializeToUtf8Bytes(request)), ct);

				long started = GetTimestamp();
				while (GetElapsedTime(started) is var elapsed && elapsed < Timeout)
				{
					var data = await ReceiveAsync(Timeout - elapsed, ct);
					if (data is null) break;

					JsonObject? response = ParsePacket(data);
					if (response?["id"] is not JsonValue rid || !rid.TryGetValue(out int got) || got != id) continue;
					if (response["error"] is { } error)
						throw new miIOException($"{method}: {error.ToJsonString()}");
					return response["result"];
				}
			}
			throw new miIOException(answered ? NoReply : Format(DeviceSilent, IP));
		}
		finally
		{
			Lock.Release();
		}
	}

	/// <summary>Hello: узнать номеръ устройства и его часы. false — устройство не отвѣтило.</summary>
	async Task<bool> HandshakeAsync(CancellationToken ct)
	{
		await Udp.SendAsync(Hello, ct);
		var data = await ReceiveAsync(Timeout, ct);
		if (data is null || data.Length < 32)
			return false;
		DeviceId = ReadUInt32BigEndian(data.AsSpan(8));
		Stamp    = ReadUInt32BigEndian(data.AsSpan(12));
		StampAt  = GetTimestamp();
		Handshaken = true;
		return true;
	}

	async Task<byte[]?> ReceiveAsync(TimeSpan timeout, CancellationToken ct)
	{
		using CancellationTokenSource cts = CreateLinkedTokenSource(ct);
		cts.CancelAfter(timeout);
		try
		{
			return (await Udp.ReceiveAsync(cts.Token)).Buffer;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			return null;
		}
		catch (SocketException)
		{
			return null; // ICMP «порт недоступенъ» и подобное — считаемъ, что отвѣта нѣтъ
		}
	}

	byte[] BuildPacket(ReadOnlySpan<byte> payload)
	{
		Span<byte> hash = stackalloc byte[16];
		byte[] packet = new byte[32 + AES.GetCiphertextLengthCbc(payload.Length, PKCS7)];
		_ = AES.EncryptCbc(payload, IV, packet.AsSpan(32), PKCS7);
		uint stamp = Stamp + (uint)GetElapsedTime(StampAt).TotalSeconds;
		WriteUInt16BigEndian(packet, 0x2131);
		WriteUInt16BigEndian(packet.AsSpan(2), (ushort)packet.Length);
		WriteUInt32BigEndian(packet.AsSpan(8), DeviceId);
		WriteUInt32BigEndian(packet.AsSpan(12), stamp);
		Token.CopyTo(packet.AsSpan(16));
		_ = HashData(packet, hash);
		hash .CopyTo(packet.AsSpan(16));
		return       packet;
	}

	JsonObject? ParsePacket(ReadOnlySpan<byte> data)
	{
		if (data.Length <= 32 || data[0] != 0x21 || data[1] != 0x31) return null;
		// пакетъ осушителя — до ~1100 байтъ (отвѣтъ не длиннѣе ~1024): буферы на стекѣ; чужой огромный — въ кучѣ
		Span<byte>   hash = stackalloc byte[16];
		Span<byte>   check = data.Length > 2048 ? new byte[data.Length]
		/**/                             : stackalloc byte[data.Length];
		data .CopyTo(check);
		Token.CopyTo(check[16..]); // контрольная сумма считается съ токеномъ на ея мѣстѣ
		_ = HashData(check, hash);
		if (!data[16..32].SequenceEqual(hash)) return null;
		try
		{
			Span<byte> plain = check[..^32]; // тотъ же буферъ: шифротекстъ уже провѣренъ, копія больше не нужна
			int length = AES.DecryptCbc(data[32..], IV, plain, PKCS7);
			return JsonNode.Parse(plain[..length].TrimEnd((byte)0)) as JsonObject;
		}
		catch (Exception e) when (e is CryptographicException or JsonException)
		{
			return null;
		}
	}

	public void Dispose()
	{
		Udp.Dispose();
		AES.Dispose();
		Lock.Dispose();
	}
}
