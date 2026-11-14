using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CertHarvester
{
	/// <summary>
	/// Модель обнаруженного в локальной сети устройства для таблицы поиска
	/// </summary>
	public class DiscoveredHostItem : System.ComponentModel.INotifyPropertyChanged
	{
		private bool _isSelected;

		/// <summary>
		/// Флаг выбора устройства для добавления в список сканирования
		/// </summary>
		public bool IsSelected
		{
			get { return _isSelected; }
			set
			{
				if (_isSelected != value)
				{
					_isSelected = value;
					OnPropertyChanged("IsSelected");
				}
			}
		}

		public string Name { get; set; }
		public string IpAddress { get; set; }
		public bool IsWindows { get; set; }
		public long PingTimeMs { get; set; }
		public string PingDisplay { get { return PingTimeMs <= 1 ? "< 1 мс" : string.Format("{0} мс", PingTimeMs); } }
		public string StatusText { get; set; }
		public string DeviceTypeIcon { get { return IsWindows ? "ПК" : "Узел"; } }

		/// <summary>
		/// Векторная иконка устройства Lucide (monitor для Windows, smartphone для остальных)
		/// </summary>
		public System.Windows.Media.ImageSource DeviceTypeImage
		{
			get
			{
				return LucideIcons.CreateImageSource(
					IsWindows ? "monitor" : "smartphone",
					14,
					IsWindows ? System.Drawing.Color.FromArgb(96, 165, 250) : System.Drawing.Color.FromArgb(245, 158, 11));
			}
		}

		public Brush StatusBrush
		{
			get
			{
				return IsWindows 
					? new SolidColorBrush(Color.FromRgb(16, 185, 129)) 
					: new SolidColorBrush(Color.FromRgb(245, 158, 11));
			}
		}

		public Brush StatusBackgroundBrush
		{
			get
			{
				return IsWindows
					? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129))
					: new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
			}
		}

		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propName)
		{
			var handler = PropertyChanged;
			if (handler != null)
			{
				handler(this, new System.ComponentModel.PropertyChangedEventArgs(propName));
			}
		}
	}

	/// <summary>
	/// Модуль сетевого сканирования рабочих станций и серверов локальной сети (NetworkScanner)
	/// Выполняет сбор сертификатов, поиск узлов и быструю диагностику связи
	/// </summary>
	public static class NetworkScanner
	{
		// Сканирование сетевого компьютера и гарантированное заполнение свойств MachineName, MachineIp и SourcePath
		public static void ScanComputer(string computerName, List<string> configuredPaths, List<CertificateRecord> records, List<NetworkComputerCredential> credentials)
		{
			CertificateScanner.ScanNetworkComputer(computerName, configuredPaths, records, credentials);
		}

		// Авторизация сетевой сессии net use к IPC$ или административным ресурсам удаленного узла
		public static bool Connect(string hostOrIp, string username, string password, out string outputMessage)
		{
			return CertificateScanner.EstablishNetworkSession(hostOrIp, username, password, out outputMessage);
		}

		// Автоматическое определение активного локального IPv4-адреса и границ подсети
		public static void GetLocalSubnetInfo(out string startIp, out string endIp, out string detectedLocalIp)
		{
			startIp = "192.168.1.1";
			endIp = "192.168.1.254";
			detectedLocalIp = "127.0.0.1";

			try
			{
				NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
				UnicastIPAddressInformation bestAddress = null;
				int bestScore = -1;

				for (int i = 0; i < interfaces.Length; i++)
				{
					NetworkInterface ni = interfaces[i];
					if (ni.OperationalStatus != OperationalStatus.Up)
					{
						continue;
					}

					if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
					{
						continue;
					}

					IPInterfaceProperties ipProps = ni.GetIPProperties();
					if (ipProps == null)
					{
						continue;
					}

					for (int u = 0; u < ipProps.UnicastAddresses.Count; u++)
					{
						UnicastIPAddressInformation uni = ipProps.UnicastAddresses[u];
						if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
						{
							continue;
						}

						if (IPAddress.IsLoopback(uni.Address))
						{
							continue;
						}

						string ipStr = uni.Address.ToString();
						if (ipStr.StartsWith("169.254.")) // APIPA
						{
							continue;
						}

						int score = 10;
						string desc = (ni.Description ?? string.Empty).ToLowerInvariant();
						string name = (ni.Name ?? string.Empty).ToLowerInvariant();

						// Физические адаптеры имеют максимальный приоритет
						if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
						{
							score = score + 50;
						}

						// Понижение приоритета виртуальных адаптеров
						if (desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vmware") || desc.Contains("radmin") || desc.Contains("vpn") || desc.Contains("tunnel") || name.Contains("vpn"))
						{
							score = score - 30;
						}

						if (ipStr.StartsWith("192.168."))
						{
							score = score + 30;
						}
						else if (ipStr.StartsWith("10."))
						{
							score = score + 20;
						}

						if (score > bestScore)
						{
							bestScore = score;
							bestAddress = uni;
						}
					}
				}

				if (bestAddress != null)
				{
					detectedLocalIp = bestAddress.Address.ToString();
					byte[] ipBytes = bestAddress.Address.GetAddressBytes();
					byte[] maskBytes = bestAddress.IPv4Mask != null ? bestAddress.IPv4Mask.GetAddressBytes() : new byte[] { 255, 255, 255, 0 };

					if (maskBytes[0] == 255 && maskBytes[1] == 255 && maskBytes[2] == 255 && maskBytes[3] == 0)
					{
						startIp = string.Format("{0}.{1}.{2}.1", ipBytes[0], ipBytes[1], ipBytes[2]);
						endIp = string.Format("{0}.{1}.{2}.254", ipBytes[0], ipBytes[1], ipBytes[2]);
					}
					else
					{
						byte[] netBytes = new byte[4];
						byte[] bcastBytes = new byte[4];
						for (int b = 0; b < 4; b++)
						{
							netBytes[b] = (byte)(ipBytes[b] & maskBytes[b]);
							bcastBytes[b] = (byte)(netBytes[b] | (~maskBytes[b]));
						}

						if (maskBytes[2] != 255)
						{
							startIp = string.Format("{0}.{1}.{2}.1", ipBytes[0], ipBytes[1], ipBytes[2]);
							endIp = string.Format("{0}.{1}.{2}.254", ipBytes[0], ipBytes[1], ipBytes[2]);
						}
						else
						{
							startIp = string.Format("{0}.{1}.{2}.{3}", netBytes[0], netBytes[1], netBytes[2], (byte)(netBytes[3] + 1));
							endIp = string.Format("{0}.{1}.{2}.{3}", bcastBytes[0], bcastBytes[1], bcastBytes[2], (byte)(bcastBytes[3] - 1));
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при расчете локальной подсети: " + ex.Message, ex);
			}
		}

		// Проверка доступности TCP-порта (SMB / Удаленный реестр) с коротким таймаутом
		public static bool IsPortOpen(string ipAddress, int port = 445, int timeoutMs = 300)
		{
			try
			{
				using (TcpClient client = new TcpClient())
				{
					IAsyncResult ar = client.BeginConnect(ipAddress, port, null, null);
					bool connected = ar.AsyncWaitHandle.WaitOne(timeoutMs);
					if (connected && client.Connected)
					{
						client.EndConnect(ar);
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		// Запрос NetBIOS-имени удаленного компьютера через UDP-запрос на порт 137
		public static string QueryNetBiosName(string ipAddress, int timeoutMs = 300)
		{
			try
			{
				byte[] queryPacket = new byte[]
				{
					0x80, 0x94, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
					0x00, 0x00, 0x00, 0x00, 0x20, 0x43, 0x4B, 0x41,
					0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
					0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
					0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
					0x41, 0x00, 0x00, 0x21, 0x00, 0x01
				};

				using (UdpClient udp = new UdpClient())
				{
					udp.Client.SendTimeout = timeoutMs;
					udp.Client.ReceiveTimeout = timeoutMs;
					IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipAddress), 137);
					udp.Send(queryPacket, queryPacket.Length, ep);

					IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
					byte[] response = udp.Receive(ref remoteEp);

					if (response != null && response.Length > 57)
					{
						int numberOfNames = response[56];
						if (numberOfNames > 0 && response.Length >= 57 + 18)
						{
							string netBiosName = System.Text.Encoding.ASCII.GetString(response, 57, 15).Trim();
							if (!string.IsNullOrWhiteSpace(netBiosName))
							{
								return netBiosName;
							}
						}
					}
				}
			}
			catch
			{
			}

			return string.Empty;
		}

		// Определение имени хоста через локальную проверку, DNS или NetBIOS
		public static string ResolveHostName(string ipAddress, string localIp, int timeoutMs = 600)
		{
			if (string.Equals(ipAddress, localIp, StringComparison.OrdinalIgnoreCase) || ipAddress == "127.0.0.1")
			{
				return Environment.MachineName;
			}

			// 1. Попытка через DNS с быстрым таймаутом
			try
			{
				Task<IPHostEntry> dnsTask = Task.Run(() => Dns.GetHostEntry(ipAddress));
				if (dnsTask.Wait(timeoutMs))
				{
					IPHostEntry entry = dnsTask.Result;
					if (entry != null && !string.IsNullOrWhiteSpace(entry.HostName))
					{
						string name = entry.HostName;
						int dotIndex = name.IndexOf('.');
						if (dotIndex > 0)
						{
							name = name.Substring(0, dotIndex);
						}
						if (!string.Equals(name, ipAddress, StringComparison.OrdinalIgnoreCase))
						{
							return name;
						}
					}
				}
			}
			catch
			{
			}

			// 2. Попытка через NetBIOS UDP 137
			string netBios = QueryNetBiosName(ipAddress, 300);
			if (!string.IsNullOrWhiteSpace(netBios))
			{
				return netBios;
			}

			return "Узел (" + ipAddress + ")";
		}

		// Быстрая комплексная проверка связи с узлом (Ping + TCP порт 445 SMB)
		public static bool CheckHostQuick(string hostOrIp, int pingTimeoutMs, int portTimeoutMs, out long pingMs, out bool isSmbOpen)
		{
			pingMs = 0;
			isSmbOpen = false;

			if (string.IsNullOrWhiteSpace(hostOrIp))
			{
				return false;
			}

			string cleanHost = hostOrIp.Trim();
			if (cleanHost.StartsWith(@"\\"))
			{
				cleanHost = cleanHost.TrimStart('\\');
			}
			int slashIdx = cleanHost.IndexOfAny(new char[] { '/', '\\' });
			if (slashIdx >= 0)
			{
				cleanHost = cleanHost.Substring(0, slashIdx);
			}

			bool pingOk = false;
			try
			{
				using (Ping ping = new Ping())
				{
					System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
					PingReply reply = ping.Send(cleanHost, pingTimeoutMs);
					sw.Stop();
					if (reply != null && reply.Status == IPStatus.Success)
					{
						pingOk = true;
						pingMs = reply.RoundtripTime > 0 ? reply.RoundtripTime : sw.ElapsedMilliseconds;
					}
				}
			}
			catch
			{
			}

			// Проверка SMB порта 445 для детекции Windows-машин
			isSmbOpen = IsPortOpen(cleanHost, 445, portTimeoutMs);

			if (pingOk || isSmbOpen)
			{
				if (pingMs == 0)
				{
					pingMs = 1;
				}
				return true;
			}

			return false;
		}

		// Асинхронное параллельное обнаружение активных компьютеров в подсети с фильтрацией Windows
		public static async Task AutoDiscoverHostsAsync(
			string startIpStr, 
			string endIpStr, 
			string localIp, 
			Action<DiscoveredHostItem> onHostFound, 
			Action<int, int> onProgress, 
			CancellationToken cancellationToken)
		{
			IPAddress startIp, endIp;
			if (!IPAddress.TryParse(startIpStr, out startIp) || !IPAddress.TryParse(endIpStr, out endIp))
			{
				throw new ArgumentException("Некорректный формат диапазона IP-адресов");
			}

			byte[] startBytes = startIp.GetAddressBytes();
			byte[] endBytes = endIp.GetAddressBytes();

			uint startNum = (uint)((startBytes[0] << 24) | (startBytes[1] << 16) | (startBytes[2] << 8) | startBytes[3]);
			uint endNum = (uint)((endBytes[0] << 24) | (endBytes[1] << 16) | (endBytes[2] << 8) | endBytes[3]);

			if (startNum > endNum)
			{
				uint tmp = startNum;
				startNum = endNum;
				endNum = tmp;
			}

			if (endNum - startNum > 1024)
			{
				endNum = startNum + 1024;
			}

			List<string> ipList = new List<string>();
			for (uint cur = startNum; cur <= endNum; cur++)
			{
				byte[] b = new byte[]
				{
					(byte)((cur >> 24) & 0xFF),
					(byte)((cur >> 16) & 0xFF),
					(byte)((cur >> 8) & 0xFF),
					(byte)(cur & 0xFF)
				};
				ipList.Add(new IPAddress(b).ToString());
			}

			int totalIps = ipList.Count;
			int processedCount = 0;

			using (SemaphoreSlim throttler = new SemaphoreSlim(40, 40))
			{
				List<Task> tasks = new List<Task>();

				for (int i = 0; i < ipList.Count; i++)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						break;
					}

					string targetIp = ipList[i];
					await throttler.WaitAsync(cancellationToken);

					Task t = Task.Run(() =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
							{
								return;
							}

							long pingMs;
							bool isSmbOpen;
							bool isAlive = CheckHostQuick(targetIp, 400, 300, out pingMs, out isSmbOpen);

							if (isAlive)
							{
								string hostName = ResolveHostName(targetIp, localIp, 600);
								DiscoveredHostItem item = new DiscoveredHostItem
								{
									IpAddress = targetIp,
									Name = hostName,
									IsWindows = isSmbOpen,
									PingTimeMs = pingMs,
									IsSelected = isSmbOpen,
									StatusText = isSmbOpen ? "Windows ПК" : "Устройство в сети"
								};

								if (onHostFound != null)
								{
									try
									{
										onHostFound(item);
									}
									catch
									{
									}
								}
							}
						}
						catch
						{
						}
						finally
						{
							int current = Interlocked.Increment(ref processedCount);
							if (onProgress != null)
							{
								try
								{
									onProgress(current, totalIps);
								}
								catch
								{
								}
							}
							throttler.Release();
						}
					}, cancellationToken);

					tasks.Add(t);
				}

				await Task.WhenAll(tasks);
			}
		}
	}
}
