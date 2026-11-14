using System;
using System.Collections.Generic;

namespace CertHarvester
{
	/// <summary>
	/// Модуль сканирования сертификатов в реестре Windows и ветках ключевых контейнеров КриптоПро CSP (RegistryCertificateScanner)
	/// Гарантирует корректное заполнение свойств MachineName, MachineIp и SourcePath для каждого сертификата
	/// </summary>
	public static class RegistryCertificateScanner
	{
		// Сканирование системных хранилищ сертификатов Windows текущего пользователя и локального компьютера
		public static void ScanWindowsStores(string computerName, List<CertificateRecord> records)
		{
			CertificateScanner.ScanWindowsCertificateStores(computerName, records);
		}

		// Сканирование реестра и ключевых носителей КриптоПро с извлечением сертификатов
		public static void ScanRegistryAndContainers(string targetHost, string targetLocalDir, List<string> downloadedFilePaths)
		{
			CertificateScanner.DownloadCertificatesFromRemoteRegistry(targetHost, targetLocalDir, downloadedFilePaths);
		}
	}
}
