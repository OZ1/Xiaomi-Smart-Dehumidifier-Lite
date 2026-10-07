using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DehumidifierControl;

using static Math;
using static StringComparison;
using static NetworkInterface;
using static OperationalStatus;
using static NetworkInterfaceType;
using static AddressFamily;

/// <summary>Мѣстная сѣть — чтобы подсказать адресъ осушителя, пока его не ввели.</summary>
static class Network
{
	static readonly string[] VitualNicKeywods = ["Virtual", "Hyper-V", "vEthernet", "VMware", "VirtualBox", "WSL", "TAP", "VPN"];

	/// <summary>Начало адреса изъ подсѣти самаго правдоподобнаго адаптера: работающій, съ частнымъ IPv4; выше — со шлюзомъ (настоящая сѣть),
	/// не виртуальный (Hyper-V, WSL, VMware, VirtualBox, VPN), затѣмъ проводной, Wi-Fi, dial-up. Октеты — цѣлые по маскѣ: /24 → «192.168.1.», /16 → «10.0.».</summary>
	public static string? LocalSubNetPrefix()
	{
		byte bestType = 0;
		bool bestGw = false, bestVirt = false;
		int bestIf = int.MaxValue;
		UnicastIPAddressInformation? best = null;
		foreach (NetworkInterface nic in GetAllNetworkInterfaces())
		{
			if (nic.OperationalStatus != Up) continue;
			if (nic.NetworkInterfaceType is Loopback or Tunnel) continue;
			IPInterfaceProperties properties = nic.GetIPProperties();
			foreach (UnicastIPAddressInformation ucast in properties.UnicastAddresses)
			{
				if (ucast.Address.AddressFamily != InterNetwork) continue;
				#pragma warning disable CS0618 // Тип или член устарел
				uint ip = unchecked((uint)ucast.Address.Address); // ReadUInt32BigEndian(.TryWriteByte(stackallock[4]))
				#pragma warning restore CS0618 // можно замѣнить на ↑
				if ((ip & 0x00FF) !=     10 && // A 10/8
					(ip & 0xF0FF) != 0x10AC && // B 172.16/12
					(ip & 0xFFFF) != 0xA8C0)   // C 192.168/16
					continue;

				bool set = best is null;

				bool v4gw = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == InterNetwork);
				if (     bestGw && !v4gw && !set) continue;
				set |=  !bestGw &&  v4gw;
				if (set) bestGw =   v4gw;

				bool v4virt = VitualNicKeywods.Any(word => nic.Description.Contains(word, OrdinalIgnoreCase) ||
				/**/                                       nic.Name       .Contains(word, OrdinalIgnoreCase));
				if (    !bestVirt &&  v4virt && !set) continue;
				set |=   bestVirt && !v4virt;
				if (set) bestVirt =   v4virt;

				byte v4type = nic.NetworkInterfaceType switch { Ethernet or Ethernet3Megabit or FastEthernetT or FastEthernetFx or GigabitEthernet => 3, Wireless80211 => 2, Ppp => 1, _ => 0 };
				if (     bestType > v4type && !set) continue;
				set |=   bestType < v4type;
				if (set) bestType = v4type;

				int v4if = properties.GetIPv4Properties().Index;
				if (     bestIf < v4if && !set) continue;
				set |=   bestIf > v4if;
				if (set) bestIf = v4if;

				best = ucast;
			}
		}
		if (best is null) return null;
		int octets = Clamp(best.PrefixLength / 8, 1, 3);
		Span<char> address = stackalloc char[15]; // 255.255.255.255
		best.Address.TryFormat(address, out int length);
		int end = 0;
		for (int octet = 0; octet < octets; octet++)
			end += address[end..length].IndexOf('.') + 1; // по точку послѣ octets-го октета включительно
		return new(address[..end]);
	}
}
