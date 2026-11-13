using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CertHarvester
{
	/// <summary>
	/// Класс сканирования файловой системы и сетевых узлов для поиска и парсинга сертификатов открытого ключа
	/// </summary>
	public static class CertificateScanner
	{
		// Массив поддерживаемых расширений открытой части сертификатов
		private static readonly string[] CertificateExtensions = new string[] { ".cer", ".crt" };

		// Главный метод выполнения комплексного сканирования локальных и сетевых ресурсов по конфигурации
		public static List<CertificateRecord> ScanAll(AppConfig config)
		{
			List<CertificateRecord> collectedRecords = new List<CertificateRecord>();

			if (config == null)
			{
				Logger.LogError("Конфигурация не инициализирована для запуска сканирования");
				return collectedRecords;
			}

			Logger.LogInfo("Начало этапа сканирования сертификатов");

			// 1. Обход директорий, указанных в конфигурационном файле
			if (config.ScanDirectories != null)
			{
				for (int i = 0; i < config.ScanDirectories.Count; i++)
				{
					string directoryPath = config.ScanDirectories[i];
					if (string.IsNullOrWhiteSpace(directoryPath))
					{
						continue;
					}

					string machineName = ExtractMachineNameFromPath(directoryPath);
					Logger.LogInfo("Сканирование каталога из конфигурации: " + directoryPath);
					ScanDirectoryRecursive(directoryPath, machineName, collectedRecords);
				}
			}

			// 2. Обход ПК из списка сетевых узлов в локальной сети
			if (config.NetworkComputers != null)
			{
				for (int i = 0; i < config.NetworkComputers.Count; i++)
				{
					string computerName = config.NetworkComputers[i];
					if (string.IsNullOrWhiteSpace(computerName))
					{
						continue;
					}

					Logger.LogInfo("Запуск сетевого сканирования компьютера: " + computerName);
					ScanNetworkComputer(computerName, config.ScanDirectories, collectedRecords);
				}
			}

			Logger.LogInfo("Сканирование завершено. Всего обнаружено сертификатов: " + collectedRecords.Count);
			return collectedRecords;
		}

		// Рекурсивный поиск файлов сертификатов в заданной локальной или сетевой директории
		public static void ScanDirectoryRecursive(string targetDirectory, string computerName, List<CertificateRecord> records)
		{
			try
			{
				if (!Directory.Exists(targetDirectory))
				{
					Logger.LogWarning("Директория не найдена или недоступна: " + targetDirectory);
					return;
				}

				// Поиск файлов сертификатов с расширениями .cer и .crt
				string[] files = Directory.GetFiles(targetDirectory);
				for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
				{
					string currentFilePath = files[fileIndex];
					string fileExtension = Path.GetExtension(currentFilePath);

					if (IsCertificateExtension(fileExtension))
					{
						CertificateRecord parsedRecord = ParseCertificateFile(currentFilePath, computerName);
						if (parsedRecord != null)
						{
							records.Add(parsedRecord);
							Logger.LogInfo("Успешно распарсен сертификат: " + parsedRecord.SerialNumber + " (" + parsedRecord.OwnerName + ")");
						}
					}
				}

				// Рекурсивный обход всех вложенных поддиректорий
				string[] subDirectories = Directory.GetDirectories(targetDirectory);
				for (int dirIndex = 0; dirIndex < subDirectories.Length; dirIndex++)
				{
					string subDir = subDirectories[dirIndex];
					ScanDirectoryRecursive(subDir, computerName, records);
				}
			}
			catch (UnauthorizedAccessException authEx)
			{
				Logger.LogError("Отказано в доступе к каталогу " + targetDirectory + ": " + authEx.Message, authEx);
			}
			catch (PathTooLongException pathEx)
			{
				Logger.LogError("Слишком длинный путь каталога " + targetDirectory + ": " + pathEx.Message, pathEx);
			}
			catch (DirectoryNotFoundException notFoundEx)
			{
				Logger.LogWarning("Каталог не найден при сканировании " + targetDirectory + ": " + notFoundEx.Message);
			}
			catch (Exception ex)
			{
				Logger.LogError("Непредвиденная ошибка при сканировании директории " + targetDirectory + ": " + ex.Message, ex);
			}
		}

		// Сканирование удаленного компьютера в локальной сети через типовые сетевые UNC-пути
		public static void ScanNetworkComputer(string computerName, List<string> configuredPaths, List<CertificateRecord> records)
		{
			string cleanHost = ExtractCleanHostName(computerName);
			bool isLocal = string.Equals(cleanHost, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(cleanHost, ".", StringComparison.OrdinalIgnoreCase) || string.Equals(cleanHost, Environment.MachineName, StringComparison.OrdinalIgnoreCase);

			if (isLocal)
			{
				Logger.LogInfo("Сетевой узел " + cleanHost + " указывает на локальный компьютер, пропуск сетевого UNC");
				return;
			}

			// Проверка доступности узла по сети через Ping
			bool isHostReachable = CheckHostAvailability(cleanHost);
			if (!isHostReachable)
			{
				Logger.LogWarning("Сетевой узел " + cleanHost + " не отвечает на ICMP-запросы (Ping)");
			}

			// Формирование типовых сетевых путей (административные и общие сетевые папки)
			List<string> networkSharePaths = new List<string>();
			networkSharePaths.Add(string.Format(@"\\{0}\c$\Certs", cleanHost));
			networkSharePaths.Add(string.Format(@"\\{0}\Certs", cleanHost));
			networkSharePaths.Add(string.Format(@"\\{0}\Certificates", cleanHost));
			networkSharePaths.Add(string.Format(@"\\{0}\c$\Users\Public", cleanHost));

			// Добавление пользовательских путей, адаптированных под сетевой компьютер
			if (configuredPaths != null)
			{
				for (int i = 0; i < configuredPaths.Count; i++)
				{
					string rawPath = configuredPaths[i];
					if (string.IsNullOrWhiteSpace(rawPath))
					{
						continue;
					}

					// Преобразование пути диска (например C:\Folder) в сетевой UNC путь \\computer\c$\Folder
					if (rawPath.Length >= 2 && rawPath[1] == ':')
					{
						char driveLetter = rawPath[0];
						string relativePart = rawPath.Substring(2).TrimStart('\\', '/');
						string uncPath = string.Format(@"\\{0}\{1}$\{2}", cleanHost, driveLetter, relativePart);
						if (!networkSharePaths.Contains(uncPath))
						{
							networkSharePaths.Add(uncPath);
						}
					}
					else if (rawPath.StartsWith(@"\\"))
					{
						if (!networkSharePaths.Contains(rawPath))
						{
							networkSharePaths.Add(rawPath);
						}
					}
				}
			}

			for (int shareIndex = 0; shareIndex < networkSharePaths.Count; shareIndex++)
			{
				string currentShare = networkSharePaths[shareIndex];
				Logger.LogInfo("Проверка сетевого пути ПК " + cleanHost + ": " + currentShare);
				ScanDirectoryRecursive(currentShare, cleanHost, records);
			}
		}

		// Извлечение чистого имени хоста или IP-адреса из строки вида 'PC-NAME (192.168.1.1)'
		private static string ExtractCleanHostName(string rawComputerName)
		{
			if (string.IsNullOrWhiteSpace(rawComputerName))
			{
				return string.Empty;
			}

			string trimmed = rawComputerName.Trim();
			if (trimmed.Contains("(") && trimmed.Contains(")"))
			{
				int startParen = trimmed.IndexOf('(');
				int endParen = trimmed.IndexOf(')');
				if (endParen > startParen)
				{
					string hostPart = trimmed.Substring(0, startParen).Trim();
					if (!string.IsNullOrEmpty(hostPart))
					{
						return hostPart;
					}
					return trimmed.Substring(startParen + 1, endParen - startParen - 1).Trim();
				}
			}

			return trimmed;
		}

		// Парсинг файла открытой части сертификата X.509 и сбор всех необходимых метаданных
		public static CertificateRecord ParseCertificateFile(string filePath, string computerName)
		{
			try
			{
				if (!File.Exists(filePath))
				{
					Logger.LogWarning("Файл сертификата не найден: " + filePath);
					return null;
				}

				byte[] certificateRawBytes = File.ReadAllBytes(filePath);
				X509Certificate2 certificate = new X509Certificate2(certificateRawBytes);

				// Извлечение серийного номера сертификата
				string rawSerialNumber = certificate.SerialNumber;
				string normalizedSerialNumber = NormalizeSerialNumber(rawSerialNumber);

				// Извлечение имени владельца (Subject) и удостоверяющего центра (Issuer)
				string ownerName = ExtractCommonName(certificate.Subject);
				string issuerName = ExtractCommonName(certificate.Issuer);

				// Проверка расширений сертификата на встроенную лицензию КриптоПро
				string licenseVersion = string.Empty;
				bool hasEmbeddedLicense = CryptoProReader.CheckEmbeddedLicense(certificate, out licenseVersion);

				// Определение версии КриптоПро на целевом ПК
				string installedCryptoProVersion = CryptoProReader.GetInstalledCryptoProVersion(computerName);
				string finalCryptoProVersion = hasEmbeddedLicense ? licenseVersion : installedCryptoProVersion;

				DateTime currentTimestamp = DateTime.Now;

				CertificateRecord record = new CertificateRecord
				{
					SerialNumber = normalizedSerialNumber,
					DateInserted = currentTimestamp,
					DateUpdated = currentTimestamp,
					OwnerName = ownerName,
					Issuer = issuerName,
					CryptoProVersion = finalCryptoProVersion,
					HasEmbeddedLicense = hasEmbeddedLicense,
					FilePath = filePath,
					ComputerName = string.IsNullOrEmpty(computerName) ? Environment.MachineName : computerName
				};

				return record;
			}
			catch (CryptographicException cryptoEx)
			{
				Logger.LogError("Криптографическая ошибка при разборе файла " + filePath + ": " + cryptoEx.Message, cryptoEx);
			}
			catch (Exception ex)
			{
				Logger.LogError("Сбой чтения и анализа сертификата " + filePath + ": " + ex.Message, ex);
			}

			return null;
		}

		// Проверка соответствия расширения файла поддерживаемым форматам открытой части сертификатов
		private static bool IsCertificateExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return false;
			}

			for (int i = 0; i < CertificateExtensions.Length; i++)
			{
				if (string.Equals(extension, CertificateExtensions[i], StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		// Нормализация серийного номера в верхний регистр без посторонних пробелов
		private static string NormalizeSerialNumber(string rawSerial)
		{
			if (string.IsNullOrEmpty(rawSerial))
			{
				return string.Empty;
			}

			return rawSerial.Replace(" ", string.Empty).Trim().ToUpperInvariant();
		}

		// Извлечение общепринятого имени (Common Name / CN) из строки Distinguished Name (DN)
		private static string ExtractCommonName(string distinguishedName)
		{
			if (string.IsNullOrWhiteSpace(distinguishedName))
			{
				return "Не указан";
			}

			string[] parts = distinguishedName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
				{
					return part.Substring(3).Trim(' ', '"', '\'');
				}
			}

			return distinguishedName.Trim();
		}

		// Извлечение имени компьютера из UNC-пути вида \\computer\share или возврат локального имени
		private static string ExtractMachineNameFromPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return Environment.MachineName;
			}

			if (path.StartsWith(@"\\"))
			{
				string trimmed = path.Substring(2);
				int nextSlashIndex = trimmed.IndexOf('\\');
				if (nextSlashIndex > 0)
				{
					return trimmed.Substring(0, nextSlashIndex);
				}
				return trimmed;
			}

			return Environment.MachineName;
		}

		// Проверка сетевой доступности узла через ICMP-запрос с коротким таймаутом
		private static bool CheckHostAvailability(string hostNameOrAddress)
		{
			try
			{
				using (Ping pingSender = new Ping())
				{
					PingReply reply = pingSender.Send(hostNameOrAddress, 1000);
					return reply != null && reply.Status == IPStatus.Success;
				}
			}
			catch
			{
				return false;
			}
		}
	}
}
