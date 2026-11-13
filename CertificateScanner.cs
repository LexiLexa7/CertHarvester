using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;

namespace CertHarvester
{
	/// <summary>
	/// Класс сканирования файловой системы, системных хранилищ Windows (certmgr / certlm) и сетевых узлов для сбора сертификатов
	/// </summary>
	public static class CertificateScanner
	{
		// Массив поддерживаемых расширений открытой части сертификатов
		private static readonly string[] CertificateExtensions = new string[] { ".cer", ".crt" };

		// Главный метод выполнения комплексного сканирования системных хранилищ Windows, локальных каталогов и сетевых узлов
		public static List<CertificateRecord> ScanAll(AppConfig config)
		{
			List<CertificateRecord> collectedRecords = new List<CertificateRecord>();

			if (config == null)
			{
				Logger.LogError("Конфигурация не инициализирована для запуска сканирования");
				return collectedRecords;
			}

			Logger.LogInfo("Начало этапа сканирования сертификатов");

			// 1. Сканирование системных хранилищ сертификатов Windows (CurrentUser\My 'Личное' и LocalMachine\My 'Личное')
			ScanWindowsCertificateStores(Environment.MachineName, collectedRecords);

			// 2. Обход директорий, указанных в конфигурационном файле
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

			// 3. Обход ПК из списка сетевых узлов в локальной сети
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

		// Сканирование системных хранилищ сертификатов Windows текущего пользователя и компьютера (certmgr.msc и certlm.msc)
		public static void ScanWindowsCertificateStores(string computerName, List<CertificateRecord> records)
		{
			try
			{
				Logger.LogInfo("Сканирование системного хранилища сертификатов Windows: Текущий пользователь / Личное (CurrentUser\\My)");
				ScanStoreByLocation(StoreLocation.CurrentUser, StoreName.My, computerName, records);

				Logger.LogInfo("Сканирование системного хранилища сертификатов Windows: Локальный компьютер / Личное (LocalMachine\\My)");
				ScanStoreByLocation(StoreLocation.LocalMachine, StoreName.My, computerName, records);

				// Сканирование физического каталога сертификатов профиля пользователя (%APPDATA%\Microsoft\SystemCertificates\My\Certificates)
				string userStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\SystemCertificates\My\Certificates");
				if (Directory.Exists(userStorePath))
				{
					Logger.LogInfo("Сканирование физического каталога системных сертификатов пользователя: " + userStorePath);
					ScanSystemCertificatesDirectory(userStorePath, "CurrentUser\\My (Файл)", computerName, records);
				}

				// Сканирование физического каталога сертификатов локального компьютера (%ProgramData%\Microsoft\Crypto\SystemCertificates\My\Certificates)
				string machineStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Crypto\SystemCertificates\My\Certificates");
				if (Directory.Exists(machineStorePath))
				{
					Logger.LogInfo("Сканирование физического каталога системных сертификатов машины: " + machineStorePath);
					ScanSystemCertificatesDirectory(machineStorePath, "LocalMachine\\My (Файл)", computerName, records);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Сбой при сканировании системных хранилищ сертификатов Windows: " + ex.Message, ex);
			}
		}

		// Сканирование конкретного системного хранилища сертификатов Windows через API X509Store
		public static void ScanStoreByLocation(StoreLocation storeLocation, StoreName storeName, string computerName, List<CertificateRecord> records)
		{
			X509Store store = null;
			try
			{
				store = new X509Store(storeName, storeLocation);
				store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

				for (int certIndex = 0; certIndex < store.Certificates.Count; certIndex++)
				{
					X509Certificate2 certificate = store.Certificates[certIndex];
					string sourceLocationDescription = string.Format("[Хранилище: {0}\\{1}]", storeLocation, storeName);
					CertificateRecord parsedRecord = CreateRecordFromCertificate(certificate, sourceLocationDescription, computerName);

					if (parsedRecord != null)
					{
						AddOrUpdateRecord(records, parsedRecord);
						Logger.LogInfo("Извлечен сертификат из хранилища (" + storeLocation + "\\" + storeName + "): " + parsedRecord.SerialNumber + " (" + parsedRecord.OwnerName + ")");
					}
				}
			}
			catch (CryptographicException cryptoEx)
			{
				Logger.LogWarning("Криптографическая ошибка при открытии хранилища (" + storeLocation + "\\" + storeName + "): " + cryptoEx.Message);
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при чтении хранилища сертификатов (" + storeLocation + "\\" + storeName + "): " + ex.Message, ex);
			}
			finally
			{
				if (store != null)
				{
					store.Close();
				}
			}
		}

		// Сканирование системного физического каталога хранения сертификатов Windows (содержащего блобы с именами хэшей)
		public static void ScanSystemCertificatesDirectory(string directoryPath, string sourcePrefix, string computerName, List<CertificateRecord> records)
		{
			try
			{
				if (!Directory.Exists(directoryPath))
				{
					return;
				}

				string[] certificateFiles = Directory.GetFiles(directoryPath);
				for (int i = 0; i < certificateFiles.Length; i++)
				{
					string filePath = certificateFiles[i];
					try
					{
						byte[] rawBytes = File.ReadAllBytes(filePath);
						if (rawBytes != null && rawBytes.Length > 0)
						{
							X509Certificate2 certificate = new X509Certificate2(rawBytes);
							CertificateRecord record = CreateRecordFromCertificate(certificate, filePath, computerName);
							if (record != null)
							{
								AddOrUpdateRecord(records, record);
								Logger.LogInfo("Обнаружен сертификат в каталоге хранилища " + sourcePrefix + ": " + record.SerialNumber + " (" + record.OwnerName + ")");
							}
						}
					}
					catch (CryptographicException)
					{
						// Файл в каталоге не является открытым сертификатом X.509
					}
					catch (Exception fileEx)
					{
						Logger.LogWarning("Не удалось прочитать системный файл сертификата " + filePath + ": " + fileEx.Message);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка обхода каталога системных сертификатов " + directoryPath + ": " + ex.Message, ex);
			}
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
							AddOrUpdateRecord(records, parsedRecord);
							Logger.LogInfo("Успешно распарсен файл сертификата: " + parsedRecord.SerialNumber + " (" + parsedRecord.OwnerName + ")");
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

		// Сканирование удаленного компьютера в локальной сети через реестр Windows и типовые сетевые UNC-пути
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

			// 1. Сканирование системного хранилища сертификатов удаленного ПК через Remote Registry
			ScanRemoteRegistryCertificateStore(cleanHost, records);

			// 2. Сканирование системных физических каталогов сертификатов удаленного ПК по UNC
			ScanRemoteSystemCertificatesDirectories(cleanHost, records);

			// 3. Формирование типовых сетевых путей (административные и общие сетевые папки)
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

		// Сканирование хранилища сертификатов удаленного компьютера через удаленный реестр Windows (Remote Registry)
		public static void ScanRemoteRegistryCertificateStore(string cleanHost, List<CertificateRecord> records)
		{
			try
			{
				Logger.LogInfo("Подключение к удаленному реестру ПК " + cleanHost + " для чтения хранилища сертификатов");
				using (RegistryKey baseKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, cleanHost))
				{
					string[] storeRegistryPaths = new string[]
					{
						@"SOFTWARE\Microsoft\SystemCertificates\MY\Certificates",
						@"SOFTWARE\Microsoft\SystemCertificates\Personal\Certificates"
					};

					for (int p = 0; p < storeRegistryPaths.Length; p++)
					{
						string currentStorePath = storeRegistryPaths[p];
						using (RegistryKey certsKey = baseKey.OpenSubKey(currentStorePath))
						{
							if (certsKey == null)
							{
								continue;
							}

							string[] certThumbprints = certsKey.GetSubKeyNames();
							for (int t = 0; t < certThumbprints.Length; t++)
							{
								string thumbprint = certThumbprints[t];
								using (RegistryKey singleCertKey = certsKey.OpenSubKey(thumbprint))
								{
									if (singleCertKey == null)
									{
										continue;
									}

									byte[] blobBytes = singleCertKey.GetValue("Blob") as byte[];
									if (blobBytes != null && blobBytes.Length > 0)
									{
										try
										{
											X509Certificate2 certificate = new X509Certificate2(blobBytes);
											string sourceDescription = string.Format(@"HKLM:\{0}\{1}", currentStorePath, thumbprint);
											CertificateRecord record = CreateRecordFromCertificate(certificate, sourceDescription, cleanHost);
											if (record != null)
											{
												AddOrUpdateRecord(records, record);
												Logger.LogInfo("Извлечен сертификат из реестра ПК " + cleanHost + ": " + record.SerialNumber + " (" + record.OwnerName + ")");
											}
										}
										catch (Exception parseEx)
										{
											Logger.LogWarning("Ошибка разбора сертификата " + thumbprint + " из реестра ПК " + cleanHost + ": " + parseEx.Message);
										}
									}
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Не удалось прочитать сертификаты из удаленного реестра ПК " + cleanHost + ": " + ex.Message);
			}
		}

		// Сканирование системных физических каталогов хранения сертификатов удаленного компьютера через UNC
		public static void ScanRemoteSystemCertificatesDirectories(string cleanHost, List<CertificateRecord> records)
		{
			try
			{
				// Сканирование хранилища LocalMachine удаленного ПК
				string remoteMachineStore = string.Format(@"\\{0}\c$\ProgramData\Microsoft\Crypto\SystemCertificates\My\Certificates", cleanHost);
				ScanSystemCertificatesDirectory(remoteMachineStore, "Удаленный LocalMachine\\My", cleanHost, records);

				// Сканирование профилей пользователей в C$\Users
				string remoteUsersDir = string.Format(@"\\{0}\c$\Users", cleanHost);
				if (Directory.Exists(remoteUsersDir))
				{
					string[] userDirs = Directory.GetDirectories(remoteUsersDir);
					for (int u = 0; u < userDirs.Length; u++)
					{
						string userProfile = userDirs[u];
						string userStore = Path.Combine(userProfile, @"AppData\Roaming\Microsoft\SystemCertificates\My\Certificates");
						if (Directory.Exists(userStore))
						{
							ScanSystemCertificatesDirectory(userStore, "Удаленный CurrentUser\\My (" + Path.GetFileName(userProfile) + ")", cleanHost, records);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Ошибка при сканировании системных каталогов сертификатов ПК " + cleanHost + ": " + ex.Message);
			}
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

				return CreateRecordFromCertificate(certificate, filePath, computerName);
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

		// Формирование стандартизированного объекта CertificateRecord из экземпляра X509Certificate2
		public static CertificateRecord CreateRecordFromCertificate(X509Certificate2 certificate, string sourcePath, string computerName)
		{
			if (certificate == null)
			{
				return null;
			}

			try
			{
				// Извлечение серийного номера сертификата
				string rawSerialNumber = certificate.SerialNumber;
				string normalizedSerialNumber = NormalizeSerialNumber(rawSerialNumber);

				// Извлечение имени владельца (Subject) и удостоверяющего центра (Issuer)
				string ownerName = ExtractCommonName(certificate.Subject);
				string issuerName = ExtractCommonName(certificate.Issuer);

				// Универсальная проверка принадлежности к СКЗИ КриптоПро и наличия лицензии
				string licenseVersion = string.Empty;
				bool isCryptoProCertificate = CryptoProReader.IsCryptoProCertificate(certificate, out licenseVersion);
				bool hasEmbeddedLicense = isCryptoProCertificate;

				// Определение версии КриптоПро: для сертификатов КриптоПро выводится версия, для остальных — прочерк
				string finalCryptoProVersion = "—";
				if (isCryptoProCertificate == true)
				{
					if (!string.IsNullOrEmpty(licenseVersion))
					{
						finalCryptoProVersion = licenseVersion;
					}
					else
					{
						finalCryptoProVersion = CryptoProReader.GetInstalledCryptoProVersion(computerName);
					}
				}

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
					FilePath = sourcePath,
					ComputerName = string.IsNullOrEmpty(computerName) ? Environment.MachineName : computerName
				};

				return record;
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при формировании записи сертификата: " + ex.Message, ex);
				return null;
			}
		}

		// Добавление новой записи сертификата в коллекцию с контролем дубликатов по серийному номеру
		public static void AddOrUpdateRecord(List<CertificateRecord> records, CertificateRecord newRecord)
		{
			if (records == null || newRecord == null || string.IsNullOrEmpty(newRecord.SerialNumber))
			{
				return;
			}

			for (int i = 0; i < records.Count; i++)
			{
				if (string.Equals(records[i].SerialNumber, newRecord.SerialNumber, StringComparison.OrdinalIgnoreCase))
				{
					// Запись уже присутствует в списке: обновляем время и сведения о лицензии
					records[i].DateUpdated = DateTime.Now;
					if (!records[i].HasEmbeddedLicense && newRecord.HasEmbeddedLicense)
					{
						records[i].HasEmbeddedLicense = true;
						records[i].CryptoProVersion = newRecord.CryptoProVersion;
					}
					return;
				}
			}

			records.Add(newRecord);
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
