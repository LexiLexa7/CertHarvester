using System;
using System.Collections.Generic;

namespace CertHarvester
{
	/// <summary>
	/// Модуль сетевого сканирования рабочих станций и серверов локальной сети (NetworkScanner)
	/// Выполняет сбор сертификатов и гарантирует заполнение свойств MachineName, MachineIp и SourcePath
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
	}
}
