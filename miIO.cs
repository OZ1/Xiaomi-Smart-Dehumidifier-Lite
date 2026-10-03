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

using static MD5;
using static Convert;
using static Encoding;
using static JsonNode;
using static IPAddress;
using static Stopwatch;
using static BinaryPrimitives;
using static CancellationTokenSource;

public sealed class miIOException(string message) : Exception(message);
public sealed class miIO : IDisposable
{
	static readonly byte[] Hello = FromHexString("21310020" + new string('f', 56));

	readonly string IP;
	readonly byte[] Token;
	readonly byte[] IV;
	readonly Aes AES = Aes.Create();
	readonly UdpClient Udp = new();
	readonly SemaphoreSlim Lock = new(1, 1);

	uint DeviceId;
	uint Stamp;
	long StampAt;
	bool Handshaken;
	int MessageId = Random.Shared.Next(1, 9000);

	public TimeSpan Timeout { get; set; } = new(0, 0, seconds: 3);

	public miIO(string ip, string tokenHex)
	{
		IP = ip;
		Token = FromHexString(tokenHex);
		if (Token.Length != 16)
			throw new ArgumentException("Токенъ долженъ состоять изъ 32 шестнадцатеричныхъ цифръ.", nameof(tokenHex));
		AES.Key = HashData(Token);
		IV = HashData([.. AES.Key, .. Token]);
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
				await Udp.SendAsync(BuildPacket(UTF8.GetBytes(request.ToJsonString())), ct);

				long started = GetTimestamp();
				while (GetElapsedTime(started) is var elapsed && elapsed < Timeout)
				{
					var data = await ReceiveAsync(Timeout - elapsed, ct);
					if (data is null) break;

					JsonObject? response = ParsePacket(data);
					if (response?["id"] is not JsonValue rid || !rid.TryGetValue<int>(out var got) || got != id) continue;
					if (response["error"] is { } error)
						throw new miIOException($"{method}: {error.ToJsonString()}");
					return response["result"];
				}
			}
			throw new miIOException(answered
				? "Нѣтъ отвѣта на команду — скорѣе всего, токенъ невѣренъ."
				: $"Устройство {IP} не отвѣчаетъ — провѣрь адресъ и сѣть.");
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

	byte[] BuildPacket(byte[] payload)
	{
		byte[] encrypted = AES.EncryptCbc(payload, IV, PaddingMode.PKCS7);
		byte[] packet = new byte[32 + encrypted.Length];
		uint stamp = Stamp + (uint)GetElapsedTime(StampAt).TotalSeconds;
		WriteUInt16BigEndian(packet, 0x2131);
		WriteUInt16BigEndian(packet.AsSpan(2), (ushort)packet.Length);
		WriteUInt32BigEndian(packet.AsSpan(8), DeviceId);
		WriteUInt32BigEndian(packet.AsSpan(12), stamp);
		Token.CopyTo(packet, 16);
		encrypted.CopyTo(packet, 32);
		HashData(packet).CopyTo(packet, 16); // контрольная сумма считается съ токеномъ на ея мѣстѣ
		return packet;
	}

	JsonObject? ParsePacket(ReadOnlySpan<byte> data)
	{
		if (data.Length <= 32 || data[0] != 0x21 || data[1] != 0x31)
			return null;
		byte[] check = [.. data];
		Token.CopyTo(check, 16);
		if (!HashData(check).AsSpan().SequenceEqual(data.Slice(16, 16)))
			return null;
		try
		{
			ReadOnlySpan<byte> plain = AES.DecryptCbc(data[32..], IV, PaddingMode.PKCS7);
			int length = plain.Length;
			while (length > 0 && plain[length - 1] == 0)
				length--;
			return JsonNode.Parse(plain[..length]) as JsonObject;
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
