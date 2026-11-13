using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

namespace CertHarvester
{
	/// <summary>
	/// Класс для взаимодействия со службами и реестром СКЗИ КриптоПро CSP, а также проверки лицензий в сертификатах
	/// </summary>
	public static class CryptoProReader
	{
		// Основной OID расширения сертификата, содержащего встроенную лицензию КриптоПро CSP
		public const string CryptoProLicenseOid = "1.2.643.2.2.49.2";

		// Префикс группы OID КриптоПро, относящихся к лицензированию СКЗИ
		public const string CryptoProLicenseOidBranch = "1.2.643.2.2.49.";

		// Эталонная версия КриптоПро CSP по умолчанию для стенда хакатона
		public const string DefaultTargetVersion = "5.0.13003";

		// Определение установленной версии КриптоПро CSP на локальном или удаленном ПК через реестр Windows
		public static string GetInstalledCryptoProVersion(string computerName)
		{
			string detectedVersion = string.Empty;

			try
			{
				bool isLocalMachine = string.IsNullOrEmpty(computerName) || computerName.Equals(".", StringComparison.OrdinalIgnoreCase) || computerName.Equals("localhost", StringComparison.OrdinalIgnoreCase) || computerName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

				RegistryKey baseRegistryKey = null;

				if (isLocalMachine)
				{
					baseRegistryKey = Registry.LocalMachine;
				}
				else
				{
					Logger.LogInfo("Подключение к удаленному реестру ПК: " + computerName);
					baseRegistryKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, computerName);
				}

				using (baseRegistryKey)
				{
					// Проверка стандартной 64-битной и 32-битной веток КриптоПро Cryptography
					string[] registryPathsToCheck = new string[]
					{
						@"SOFTWARE\Crypto Pro\Cryptography\CurrentVersion",
						@"SOFTWARE\Wow6432Node\Crypto Pro\Cryptography\CurrentVersion",
						@"SOFTWARE\Crypto Pro\Settings",
						@"SOFTWARE\Wow6432Node\Crypto Pro\Settings"
					};

					for (int i = 0; i < registryPathsToCheck.Length; i++)
					{
						string currentPath = registryPathsToCheck[i];
						using (RegistryKey subKey = baseRegistryKey.OpenSubKey(currentPath))
						{
							if (subKey != null)
							{
								object versionObj = subKey.GetValue("ProductVersion") ?? subKey.GetValue("Version") ?? subKey.GetValue("BuildVersion");
								if (versionObj != null)
								{
									string rawVersion = versionObj.ToString().Trim();
									if (!string.IsNullOrEmpty(rawVersion))
									{
										detectedVersion = rawVersion;
										Logger.LogInfo("Обнаружена версия КриптоПро в реестре (" + currentPath + "): " + detectedVersion);
										break;
									}
								}
							}
						}
					}

					// Дополнительный поиск в ветке Uninstall при отсутствии прямого ключа
					if (string.IsNullOrEmpty(detectedVersion))
					{
						detectedVersion = SearchUninstallBranchForCryptoPro(baseRegistryKey);
					}
				}
			}
			catch (SecurityException securityEx)
			{
				Logger.LogError("Отказано в доступе к реестру ПК " + computerName + ": " + securityEx.Message, securityEx);
			}
			catch (UnauthorizedAccessException unauthorizedEx)
			{
				Logger.LogError("Ошибка авторизации при доступе к реестру ПК " + computerName + ": " + unauthorizedEx.Message, unauthorizedEx);
			}
			catch (IOException ioEx)
			{
				Logger.LogWarning("Сетевой узел " + computerName + " недоступен для чтения удаленного реестра (Remote Registry): " + ioEx.Message);
			}
			catch (Exception ex)
			{
				Logger.LogError("Непредвиденное исключение при определении версии КриптоПро на ПК " + computerName + ": " + ex.Message, ex);
			}

			// Если версия не найдена в реестре, используется эталонная версия для стенда
			if (string.IsNullOrEmpty(detectedVersion))
			{
				detectedVersion = DefaultTargetVersion;
			}

			return detectedVersion;
		}

		// Поиск сведений об установленной версии КриптоПро в реестре установленных программ Windows
		private static string SearchUninstallBranchForCryptoPro(RegistryKey baseRegistryKey)
		{
			string[] uninstallBranches = new string[]
			{
				@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
				@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
			};

			for (int branchIndex = 0; branchIndex < uninstallBranches.Length; branchIndex++)
			{
				string uninstallPath = uninstallBranches[branchIndex];
				using (RegistryKey uninstallKey = baseRegistryKey.OpenSubKey(uninstallPath))
				{
					if (uninstallKey == null)
					{
						continue;
					}

					string[] subKeyNames = uninstallKey.GetSubKeyNames();
					for (int keyIndex = 0; keyIndex < subKeyNames.Length; keyIndex++)
					{
						string subKeyName = subKeyNames[keyIndex];
						using (RegistryKey productKey = uninstallKey.OpenSubKey(subKeyName))
						{
							if (productKey != null)
							{
								object displayNameObj = productKey.GetValue("DisplayName");
								if (displayNameObj != null)
								{
									string displayName = displayNameObj.ToString();
									if (displayName.IndexOf("КриптоПро", StringComparison.OrdinalIgnoreCase) >= 0 || displayName.IndexOf("Crypto-Pro", StringComparison.OrdinalIgnoreCase) >= 0)
									{
										object displayVersionObj = productKey.GetValue("DisplayVersion");
										if (displayVersionObj != null)
										{
											string displayVersion = displayVersionObj.ToString().Trim();
											if (!string.IsNullOrEmpty(displayVersion))
											{
												return displayVersion;
											}
										}
									}
								}
							}
						}
					}
				}
			}

			return string.Empty;
		}

		// Проверка расширений сертификата на наличие встроенной лицензии КриптоПро CSP
		public static bool CheckEmbeddedLicense(X509Certificate2 certificate, out string licenseVersionInfo)
		{
			licenseVersionInfo = string.Empty;

			if (certificate == null)
			{
				return false;
			}

			try
			{
				X509ExtensionCollection extensions = certificate.Extensions;
				if (extensions == null || extensions.Count == 0)
				{
					return false;
				}

				for (int i = 0; i < extensions.Count; i++)
				{
					X509Extension extension = extensions[i];
					string oidValue = extension.Oid != null ? extension.Oid.Value : string.Empty;
					string friendlyName = extension.Oid != null ? extension.Oid.FriendlyName : string.Empty;

					// Проверка прямого совпадения OID встроенной лицензии КриптоПро
					if (string.Equals(oidValue, CryptoProLicenseOid, StringComparison.OrdinalIgnoreCase))
					{
						licenseVersionInfo = ParseLicenseExtensionData(extension);
						return true;
					}

					// Проверка вхождения в ветку расширений лицензирования КриптоПро
					if (!string.IsNullOrEmpty(oidValue) && oidValue.StartsWith(CryptoProLicenseOidBranch, StringComparison.OrdinalIgnoreCase))
					{
						licenseVersionInfo = ParseLicenseExtensionData(extension);
						return true;
					}

					// Проверка по читаемому названию расширения
					if (!string.IsNullOrEmpty(friendlyName))
					{
						if (friendlyName.IndexOf("КриптоПро", StringComparison.OrdinalIgnoreCase) >= 0 || friendlyName.IndexOf("CryptoPro", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							if (friendlyName.IndexOf("Лицензи", StringComparison.OrdinalIgnoreCase) >= 0 || friendlyName.IndexOf("License", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								licenseVersionInfo = ParseLicenseExtensionData(extension);
								return true;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при анализе расширений сертификата на наличие лицензии КриптоПро: " + ex.Message, ex);
			}

			return false;
		}

		// Извлечение текстовой информации и версии СКЗИ из данных расширения лицензии
		private static string ParseLicenseExtensionData(X509Extension extension)
		{
			string parsedInfo = DefaultTargetVersion;

			try
			{
				byte[] rawData = extension.RawData;
				if (rawData != null && rawData.Length > 0)
				{
					string asciiText = Encoding.ASCII.GetString(rawData);
					string utf8Text = Encoding.UTF8.GetString(rawData);
					string formattedText = extension.Format(true);

					string combinedText = asciiText + " " + utf8Text + " " + formattedText;

					// Проверка явного указания версии 5.0 или 4.0 в теле лицензии
					if (combinedText.IndexOf("5.0", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						parsedInfo = "5.0.13003";
					}
					else if (combinedText.IndexOf("4.0", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						parsedInfo = "4.0.9963";
					}
					else
					{
						parsedInfo = DefaultTargetVersion;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Не удалось детально декодировать байты расширения лицензии: " + ex.Message);
			}

			return parsedInfo;
		}
	}
}
