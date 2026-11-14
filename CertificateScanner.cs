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
	/// Класс сканирования файловой системы, системных хранилищ Windows, удаленного подключения к ПК сети и скачивания сертификатов
	/// </summary>
	public static class CertificateScanner
	{
		// Массив поддерживаемых расширений открытой части сертификатов
		private static readonly string[] CertificateExtensions = new string[] { ".cer", ".crt" };

		// Корневой каталог на компьютере администратора для локального сохранения скачанных из сети сертификатов
		public static readonly string HarvestedCertificatesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HarvestedCertificates");

		// Главный метод выполнения комплексного сканирования системных хранилищ Windows, локальных каталогов и сетевых узлов
		public static List<CertificateRecord> ScanAll(AppConfig config)
		{
			List<CertificateRecord> collectedRecords = new List<CertificateRecord>();

			if (config == null)
			{
				Logger.LogError("Конфигурация не инициализирована для запуска сканирования");
				return collectedRecords;
			}

			// Создание локального каталога для скачивания сертификатов при его отсутствии
			EnsureHarvestDirectoryExists();

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

			// 3. Обход ПК из списка сетевых узлов в локальной сети: подключение, скачивание и парсинг
			if (config.NetworkComputers != null)
			{
				for (int i = 0; i < config.NetworkComputers.Count; i++)
				{
					string computerName = config.NetworkComputers[i];
					if (string.IsNullOrWhiteSpace(computerName))
					{
						continue;
					}

					Logger.LogInfo("Запуск процедуры сбора сертификатов с сетевого ПК: " + computerName);
					ScanNetworkComputer(computerName, config.ScanDirectories, collectedRecords);
				}
			}

			Logger.LogInfo("Сканирование завершено. Всего обнаружено сертификатов: " + collectedRecords.Count);
			return collectedRecords;
		}

		// Проверка существования и создание базового каталога для сохранения скачанных сертификатов
		private static void EnsureHarvestDirectoryExists()
		{
			try
			{
				if (!Directory.Exists(HarvestedCertificatesDirectory))
				{
					Directory.CreateDirectory(HarvestedCertificatesDirectory);
					Logger.LogInfo("Создан локальный каталог для сбора и архивации сертификатов: " + HarvestedCertificatesDirectory);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Не удалось создать каталог HarvestedCertificates: " + ex.Message);
			}
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

						// Локальное резервное архивирование сертификата в каталог HarvestedCertificates
						ArchiveCertificateLocally(certificate, computerName, "Store_" + storeLocation);
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

								// Сохранение копии сертификата в локальный архив
								ArchiveCertificateLocally(certificate, computerName, "SystemFolder");
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

		// Подключение к сетевому ПК, скачивание сертификатов на локальный компьютер и их последующий парсинг
		public static void ScanNetworkComputer(string computerName, List<string> configuredPaths, List<CertificateRecord> records)
		{
			string cleanHost = ExtractCleanHostName(computerName);
			if (string.IsNullOrWhiteSpace(cleanHost))
			{
				return;
			}

			// Создание локального каталога на компьютере администратора для хранения скачанных с данного ПК сертификатов
			string computerLocalDirectory = Path.Combine(HarvestedCertificatesDirectory, SanitizeFolderName(cleanHost));
			if (!Directory.Exists(computerLocalDirectory))
			{
				Directory.CreateDirectory(computerLocalDirectory);
			}

			bool isLocal = string.Equals(cleanHost, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(cleanHost, ".", StringComparison.OrdinalIgnoreCase) || string.Equals(cleanHost, Environment.MachineName, StringComparison.OrdinalIgnoreCase);

			if (isLocal == true)
			{
				Logger.LogInfo("Сетевой узел " + cleanHost + " является локальным компьютером. Выполняется сканирование локальных хранилищ");
				return;
			}

			// Проверка доступности удаленного узла по ICMP Ping
			bool isHostReachable = CheckHostAvailability(cleanHost);
			if (isHostReachable == false)
			{
				Logger.LogWarning("Сетевой узел " + cleanHost + " не отвечает на ICMP-запросы (Ping)");
			}

			Logger.LogInfo("Подключение к списку сертификатов удаленного компьютера: " + cleanHost);

			// Список путей локально сохраненных файлов сертификатов, скачанных с удаленного ПК
			List<string> downloadedFilePaths = new List<string>();

			// 1. Подключение к удаленному реестру (Remote Registry) и скачивание сертификатов
			DownloadCertificatesFromRemoteRegistry(cleanHost, computerLocalDirectory, downloadedFilePaths);

			// 2. Подключение к системным каталогам удаленного ПК по сетевым UNC-путям и скачивание сертификатов
			DownloadCertificatesFromRemoteUncSystemStores(cleanHost, computerLocalDirectory, downloadedFilePaths);

			// 3. Подключение к пользовательским и общедоступным директориям удаленного ПК по UNC и скачивание .cer/.crt
			DownloadCertificatesFromRemoteUncShares(cleanHost, configuredPaths, computerLocalDirectory, downloadedFilePaths);

			Logger.LogInfo("Завершено скачивание с ПК " + cleanHost + ". Локально сохранено файлов: " + downloadedFilePaths.Count);

			// 4. Последующий парсинг всех скачанных сертификатов из локальной папки
			if (downloadedFilePaths.Count > 0)
			{
				for (int f = 0; f < downloadedFilePaths.Count; f++)
				{
					string localCertPath = downloadedFilePaths[f];
					CertificateRecord record = ParseCertificateFile(localCertPath, cleanHost);
					if (record != null)
					{
						AddOrUpdateRecord(records, record);
						Logger.LogInfo("Успешно распарсен скачанный сертификат ПК " + cleanHost + ": " + record.SerialNumber + " (" + record.OwnerName + ")");
					}
				}
			}
			else
			{
				// Если в сети ПК недоступен, проверяем наличие ранее скачанных сертификатов в локальном кэше
				string[] cachedFiles = Directory.GetFiles(computerLocalDirectory, "*.*");
				int parsedFromCache = 0;

				for (int c = 0; c < cachedFiles.Length; c++)
				{
					string cachedFile = cachedFiles[c];
					CertificateRecord record = ParseCertificateFile(cachedFile, cleanHost);
					if (record != null)
					{
						AddOrUpdateRecord(records, record);
						parsedFromCache = parsedFromCache + 1;
					}
				}

				if (parsedFromCache > 0)
				{
					Logger.LogInfo("Загружено из локального кэша ранее скачанных сертификатов ПК " + cleanHost + ": " + parsedFromCache);
				}
			}
		}

		// Подключение к удаленному реестру Windows (Remote Registry) и скачивание найденных сертификатов в локальные файлы .cer
		public static void DownloadCertificatesFromRemoteRegistry(string cleanHost, string targetLocalDir, List<string> downloadedPaths)
		{
			try
			{
				Logger.LogInfo("Подключение к удаленному реестру ПК " + cleanHost + " для выгрузки сертификатов");
				using (RegistryKey baseKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, cleanHost))
				{
					string[] storeRegistryPaths = new string[]
					{
						@"SOFTWARE\Microsoft\SystemCertificates\MY\Certificates",
						@"SOFTWARE\Microsoft\SystemCertificates\Personal\Certificates",
						@"SOFTWARE\Microsoft\SystemCertificates\Root\Certificates",
						@"SOFTWARE\Microsoft\SystemCertificates\CA\Certificates"
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
											X509Certificate2 cert = new X509Certificate2(blobBytes);
											byte[] rawExport = cert.Export(X509ContentType.Cert);

											string safeSerial = NormalizeSerialNumber(cert.SerialNumber);
											string safeOwner = SanitizeFileName(ExtractCommonName(cert.Subject));
											string fileName = string.Format("RemoteStore_{0}_{1}.cer", safeSerial, safeOwner);
											string localFilePath = Path.Combine(targetLocalDir, fileName);

											File.WriteAllBytes(localFilePath, rawExport);
											if (!downloadedPaths.Contains(localFilePath))
											{
												downloadedPaths.Add(localFilePath);
											}

											Logger.LogInfo("Скачан сертификат из реестра ПК " + cleanHost + ": " + fileName + " (" + cert.Subject + ")");
										}
										catch (Exception parseEx)
										{
											Logger.LogWarning("Ошибка разбора блоба сертификата " + thumbprint + " из реестра ПК " + cleanHost + ": " + parseEx.Message);
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
				Logger.LogWarning("Не удалось выгрузить сертификаты через Remote Registry ПК " + cleanHost + ": " + ex.Message);
			}
		}

		// Подключение к системным каталогам хранения сертификатов удаленного ПК по UNC и скачивание файлов
		public static void DownloadCertificatesFromRemoteUncSystemStores(string cleanHost, string targetLocalDir, List<string> downloadedPaths)
		{
			try
			{
				// 1. Скачивание сертификатов из хранилища машины LocalMachine
				string remoteMachineStore = string.Format(@"\\{0}\c$\ProgramData\Microsoft\Crypto\SystemCertificates\My\Certificates", cleanHost);
				DownloadCertificateFilesFromDirectory(remoteMachineStore, "LocalMachine", cleanHost, targetLocalDir, downloadedPaths);

				// 2. Скачивание сертификатов из профилей пользователей в C$\Users
				string remoteUsersDir = string.Format(@"\\{0}\c$\Users", cleanHost);
				if (Directory.Exists(remoteUsersDir))
				{
					string[] userDirs = Directory.GetDirectories(remoteUsersDir);
					for (int u = 0; u < userDirs.Length; u++)
					{
						string userProfile = userDirs[u];
						string userName = Path.GetFileName(userProfile);
						string userStore = Path.Combine(userProfile, @"AppData\Roaming\Microsoft\SystemCertificates\My\Certificates");
						if (Directory.Exists(userStore))
						{
							DownloadCertificateFilesFromDirectory(userStore, "User_" + userName, cleanHost, targetLocalDir, downloadedPaths);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Ошибка при скачивании сертификатов из системных UNC-папок ПК " + cleanHost + ": " + ex.Message);
			}
		}

		// Подключение к пользовательским сетевым папкам и UNC-ресурсам удаленного ПК и скачивание сертификатов
		public static void DownloadCertificatesFromRemoteUncShares(string cleanHost, List<string> configuredPaths, string targetLocalDir, List<string> downloadedPaths)
		{
			try
			{
				List<string> candidateSharePaths = new List<string>();
				candidateSharePaths.Add(string.Format(@"\\{0}\c$\Certificates", cleanHost));
				candidateSharePaths.Add(string.Format(@"\\{0}\c$\Certs", cleanHost));
				candidateSharePaths.Add(string.Format(@"\\{0}\Certificates", cleanHost));
				candidateSharePaths.Add(string.Format(@"\\{0}\c$\CryptoPro\Certs", cleanHost));
				candidateSharePaths.Add(string.Format(@"\\{0}\c$\Users\Public", cleanHost));

				if (configuredPaths != null)
				{
					for (int i = 0; i < configuredPaths.Count; i++)
					{
						string rawPath = configuredPaths[i];
						if (string.IsNullOrWhiteSpace(rawPath))
						{
							continue;
						}

						if (rawPath.Length >= 2 && rawPath[1] == ':')
						{
							char driveLetter = rawPath[0];
							string relativePart = rawPath.Substring(2).TrimStart('\\', '/');
							string uncPath = string.Format(@"\\{0}\{1}$\{2}", cleanHost, driveLetter, relativePart);
							if (!candidateSharePaths.Contains(uncPath))
							{
								candidateSharePaths.Add(uncPath);
							}
						}
						else if (rawPath.StartsWith(@"\\"))
						{
							if (!candidateSharePaths.Contains(rawPath))
							{
								candidateSharePaths.Add(rawPath);
							}
						}
					}
				}

				for (int p = 0; p < candidateSharePaths.Count; p++)
				{
					string sharePath = candidateSharePaths[p];
					DownloadCertificateFilesFromDirectory(sharePath, "Share", cleanHost, targetLocalDir, downloadedPaths);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Ошибка при обходе сетевых UNC-ресурсов ПК " + cleanHost + ": " + ex.Message);
			}
		}

		// Вспомогательный метод копирования (скачивания) сертификатов из удаленной директории в локальную папку
		private static void DownloadCertificateFilesFromDirectory(string remoteDirectory, string sourcePrefix, string cleanHost, string targetLocalDir, List<string> downloadedPaths)
		{
			try
			{
				if (!Directory.Exists(remoteDirectory))
				{
					return;
				}

				string[] files = Directory.GetFiles(remoteDirectory);
				for (int i = 0; i < files.Length; i++)
				{
					string remoteFilePath = files[i];
					try
					{
						byte[] fileBytes = File.ReadAllBytes(remoteFilePath);
						if (fileBytes != null && fileBytes.Length > 0)
						{
							// Проверка валидности сертификата X.509
							X509Certificate2 cert = new X509Certificate2(fileBytes);
							string safeSerial = NormalizeSerialNumber(cert.SerialNumber);
							string safeOwner = SanitizeFileName(ExtractCommonName(cert.Subject));

							string localFileName = string.Format("{0}_{1}_{2}.cer", sourcePrefix, safeSerial, safeOwner);
							string localDestinationPath = Path.Combine(targetLocalDir, localFileName);

							File.WriteAllBytes(localDestinationPath, fileBytes);
							if (!downloadedPaths.Contains(localDestinationPath))
							{
								downloadedPaths.Add(localDestinationPath);
							}

							Logger.LogInfo("Скачан сертификат с ПК " + cleanHost + " [" + sourcePrefix + "]: " + localFileName);
						}
					}
					catch (CryptographicException)
					{
						// Файл не является сертификатом X.509, пропуск
					}
					catch (Exception copyEx)
					{
						Logger.LogWarning("Не удалось скопировать файл сертификата " + remoteFilePath + ": " + copyEx.Message);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Каталог " + remoteDirectory + " недоступен для чтения: " + ex.Message);
			}
		}

		// Локальное сохранение копии сертификата в каталог HarvestedCertificates для создания единого архива
		public static void ArchiveCertificateLocally(X509Certificate2 certificate, string computerName, string sourceTag)
		{
			try
			{
				if (certificate == null)
				{
					return;
				}

				string targetFolder = Path.Combine(HarvestedCertificatesDirectory, SanitizeFolderName(computerName));
				if (!Directory.Exists(targetFolder))
				{
					Directory.CreateDirectory(targetFolder);
				}

				string safeSerial = NormalizeSerialNumber(certificate.SerialNumber);
				string safeOwner = SanitizeFileName(ExtractCommonName(certificate.Subject));
				string fileName = string.Format("{0}_{1}_{2}.cer", sourceTag, safeSerial, safeOwner);
				string fullPath = Path.Combine(targetFolder, fileName);

				if (!File.Exists(fullPath))
				{
					byte[] rawBytes = certificate.Export(X509ContentType.Cert);
					File.WriteAllBytes(fullPath, rawBytes);
				}
			}
			catch
			{
				// Ошибки локального архивирования не прерывают основной процесс сканирования
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

		// Очистка строки от недопустимых символов имени файла в ОС Windows
		private static string SanitizeFileName(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				return "Certificate";
			}

			char[] invalidChars = Path.GetInvalidFileNameChars();
			string result = input;
			for (int i = 0; i < invalidChars.Length; i++)
			{
				result = result.Replace(invalidChars[i], '_');
			}

			return result.Trim();
		}

		// Очистка строки от недопустимых символов имени каталога в ОС Windows
		private static string SanitizeFolderName(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				return "Computer";
			}

			char[] invalidChars = Path.GetInvalidPathChars();
			string result = input;
			for (int i = 0; i < invalidChars.Length; i++)
			{
				result = result.Replace(invalidChars[i], '_');
			}

			result = result.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
			return result.Trim();
		}
	}
}
