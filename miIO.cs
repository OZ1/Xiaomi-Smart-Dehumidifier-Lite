#pragma warning disable IDE1006 // Нарушение правила именования: Эти слова должны начинаться с прописных символов: siid
using System.Buffers.Binary;
using System.Collections.Concurrent;
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
using static Task;
using static String;
using static Convert;
using static JsonNode;
using static IPAddress;
using static Stopwatch;
using static Enumerable;
using static Interlocked;
using static PaddingMode;
using static JsonSerializer;
using static BinaryPrimitives;
using static TaskCreationOptions;

public sealed class miIOException(string message) : Exception(message);
public sealed class miIO : IDisposable
{
	/// <summary>Сколько командъ осушитель держитъ въ работѣ: на восьмую одновременную отвѣчаетъ busy (провѣрено: изъ 10 сразу — 7 отвѣтовъ, 3 busy).
	/// Берёмъ съ запасомъ.</summary>
	const int MaxInFlight = 4;

	static readonly byte[] Hello = [0x21,0x31, 0,32, .. Repeat<byte>(0xFF, 28)];

	readonly string IP;                      // адресъ осушителя — для сообщенія, что онъ не отвѣчаетъ
	readonly byte[] Token;                   // 16 байтъ: изъ него ключъ и векторъ; онъ же стоитъ на мѣстѣ контрольной суммы, пока она считается
	readonly byte[] IV;                      // 16 байтъ: векторъ AES-CBC = MD5(ключъ + токенъ)
	readonly Aes AES = Aes.Create();         // 16 байтъ  ключъ   AES-128 = MD5(токенъ)
	readonly UdpClient Udp = new();          // портъ 54321
	readonly SemaphoreSlim HelloLock = new(1, 1); // hello по одному: отвѣтъ на него безъ id, и ждётъ его одинъ HelloWaiter
	readonly SemaphoreSlim InFlight = new(MaxInFlight, MaxInFlight); // команды въ пути, не больше MaxInFlight
	readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> Pending = new(); // посланныя команды по id; цикл пріёма раздаётъ имъ отвѣты
	readonly CancellationTokenSource Closing = new(); // останавливаетъ цикл пріёма въ Dispose
	TaskCompletionSource<byte[]>? HelloWaiter; // ждётъ отвѣта на hello, пока тотъ въ пути

	uint DeviceId;   // номеръ устройства изъ отвѣта на hello — пишется въ каждый пакетъ
	uint Stamp;      // часы устройства изъ отвѣта на hello, секунды
	long StampAt;    // когда пришёлъ Stamp (Stopwatch): часы устройства досчитываемъ сами — пакетъ съ меткой изъ прошлаго оно отбрасываетъ
	bool Handshaken; // hello прошёлъ: номеръ и часы извѣстны
	int  MessageId = Random.Shared.Next(1, 9000); // id послѣдней команды; начало случайное, чтобы запоздалый отвѣтъ прошлаго запуска не совпалъ съ нашимъ

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
		_ = Run(ReceiveLoopAsync); // циклъ пріёма — сразу въ пулѣ потоковъ, не на потокѣ создателя (въ окнѣ это UI-потокъ)
	}

	/// <summary>Команды не ждутъ другъ друга: каждая кладётся въ Pending подъ своимъ id и ждётъ, пока цикл пріёма отдастъ ей отвѣтъ.</summary>
	public async Task<JsonNode?> SendAsync(string method, JsonNode? parameters = null, CancellationToken ct = default)
	{
		await InFlight.WaitAsync(ct).ConfigureAwait(false); // здѣсь и далѣе — не возвращаться въ контекстъ вызвавшаго: въ UI вернётся только его собственный await
		try
		{
			int busy = 0;
			bool answered = Handshaken; // отвѣчало ли устройство на hello — чтобы вѣрно назвать причину неудачи
			bool hello   = !Handshaken;
			for (int attempt = 0; attempt < 3; attempt++)
			{
				if (hello)
				{
					if (!await HandshakeAsync(ct).ConfigureAwait(false)) continue; // осушитель иногда пропускаетъ hello — это одна попытка, а не конецъ
					answered = true;
					hello = false;
				}

				int id = Increment(ref MessageId);
				JsonObject request = new()
				{
					["id"] = id,
					["method"] = method,
					["params"] = parameters?.DeepClone() ?? new JsonArray(),
				};
				TaskCompletionSource<JsonObject> waiter = new(RunContinuationsAsynchronously);
				Pending[id] = waiter;
				JsonObject response;
				try
				{
					await Udp.SendAsync(BuildPacket(SerializeToUtf8Bytes(request)), ct).ConfigureAwait(false);
					response = await waiter.Task.WaitAsync(Timeout, ct).ConfigureAwait(false);
				}
				catch (TimeoutException)
				{
					hello = true; // слѣдующая попытка — съ новымъ hello
					continue;
				}
				finally
				{
					Pending.TryRemove(id, out _); // отвѣтъ, пришедшій послѣ, уже некому отдать — пропадётъ
				}
				if (response["error"] is { } error)
				{
					// Кодъ отвѣта «занятъ»: {"code":-30012,"message":"busy."} — команда не выполнена, её можно повторить.
					if (error["code"] is JsonValue c && c.TryGetValue(out int code) && code == -30012 && busy++ < 10)
					{
						await Delay(100/*мс*/, ct).ConfigureAwait(false);
						attempt--; // не считаемъ попыткой, потому что команда не дошла до осушителя
						continue;
					}
					throw new miIOException($"{method}: {error.ToJsonString()}");
				}
				return response["result"];
			}
			throw new miIOException(answered ? NoReply : Format(DeviceSilent, IP));
		}
		finally
		{
			InFlight.Release();
		}
	}

	/// <summary>Hello: узнать номеръ устройства и его часы. false — устройство не отвѣтило.</summary>
	async Task<bool> HandshakeAsync(CancellationToken ct)
	{
		await HelloLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			TaskCompletionSource<byte[]> waiter = new(RunContinuationsAsynchronously);
			HelloWaiter = waiter;
			await Udp.SendAsync(Hello, ct).ConfigureAwait(false);
			byte[] data;
			try
			{
				data = await waiter.Task.WaitAsync(Timeout, ct).ConfigureAwait(false);
			}
			catch (TimeoutException)
			{
				return false;
			}
			finally
			{
				HelloWaiter = null;
			}
			DeviceId = ReadUInt32BigEndian(data.AsSpan(8));
			Stamp    = ReadUInt32BigEndian(data.AsSpan(12));
			StampAt  = GetTimestamp();
			Handshaken = true;
			return true;
		}
		finally
		{
			HelloLock.Release();
		}
	}

	/// <summary>Единственный читатель сокета: отвѣтъ на hello (голый заголовокъ, 32 байта) — тому, кто ждётъ hello,
	/// отвѣтъ на команду — той, чей id въ нёмъ; чужое и битое пропускается.</summary>
	async Task ReceiveLoopAsync()
	{
		while (!Closing.IsCancellationRequested)
		{
			byte[] data;
			try
			{
				data = (await Udp.ReceiveAsync(Closing.Token).ConfigureAwait(false)).Buffer;
			}
			catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
			{
				return; // Dispose
			}
			catch (SocketException)
			{
				continue; // ICMP «порт недоступенъ» и подобное — отвѣта нѣтъ, ждущіе дождутся своего Timeout
			}
			if (data.Length == 32)
				HelloWaiter?.TrySetResult(data);
			else if (ParsePacket(data) is { } response && response["id"] is JsonValue rid && rid.TryGetValue(out int id) && Pending.TryRemove(id, out var waiter))
				waiter.TrySetResult(response);
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
		Closing.Cancel(); // цикл пріёма выходитъ
		Udp.Dispose();
		AES.Dispose();
		HelloLock.Dispose();
		InFlight.Dispose();
		Closing.Dispose();
	}
}
