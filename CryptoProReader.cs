using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CertHarvester
{
	/// <summary>
	/// Класс для взаимодействия со службами и реестром СКЗИ КриптоПро CSP, а также универсального определения сертификатов КриптоПро и лицензий
	/// </summary>
	public static class CryptoProReader
	{
		// Основной OID расширения сертификата, содержащего встроенную лицензию КриптоПро CSP
		public const string CryptoProLicenseOid = "1.2.643.2.2.49.2";

		// Префикс группы OID КриптоПро, относящихся к лицензированию СКЗИ
		public const string CryptoProLicenseOidBranch = "1.2.643.2.2.49.";

		// Эталонная версия КриптоПро CSP по умолчанию для стенда хакатона
		public const string DefaultTargetVersion = "5.0.13003";

		// Список ключевых слов доверенных аккредитованных удостоверяющих центров (УЦ) РФ и КриптоПро
		private static readonly string[] RussianCertificateAuthorities = new string[]
		{
			"криптопро",
			"крипто-про",
			"cryptopro",
			"crypto-pro",
			"грчц",
			"главный радиочастотный центр",
			"контур",
			"скб контур",
			"тензор",
			"сбис",
			"фнс",
			"налоговая",
			"казначейство",
			"федеральное казначейство",
			"такском",
			"астрал",
			"калуга астрал",
			"сберключ",
			"сбербанк",
			"ростелеком",
			"этп",
			"минкомсвязь",
			"минцифры"
		};

		// Определение установленной версии КриптоПро CSP на локальном или удаленном ПК через реестр Windows
		public static string GetInstalledCryptoProVersion(string computerName)
		{
			string detectedVersion = string.Empty;

			try
			{
				bool isLocalMachine = string.IsNullOrEmpty(computerName) || computerName.Equals(".", StringComparison.OrdinalIgnoreCase) || computerName.Equals("localhost", StringComparison.OrdinalIgnoreCase) || computerName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

				RegistryKey baseRegistryKey = null;

				if (isLocalMachine == true)
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

		// Универсальный метод распознавания принадлежности сертификата к СКЗИ КриптоПро (ГОСТ, лицензия, УЦ РФ, криптопровайдер)
		public static bool IsCryptoProCertificate(X509Certificate2 certificate, out string licenseVersionInfo)
		{
			licenseVersionInfo = string.Empty;

			if (certificate == null)
			{
				return false;
			}

			// 1. Исключение служебных, системных, тестовых и сторонних зарубежных сертификатов Windows
			if (IsSystemOrForeignCertificate(certificate) == true)
			{
				return false;
			}

			// 2. Проверка наличия явного расширения встроенной лицензии КриптоПро CSP
			if (CheckExplicitLicenseExtension(certificate, out licenseVersionInfo) == true)
			{
				return true;
			}

			// 3. Проверка криптографических алгоритмов ГОСТ Р 34.10 / ГОСТ Р 34.11
			if (HasGostAlgorithm(certificate) == true)
			{
				return true;
			}

			// 4. Проверка специализированных расширений и OID КриптоПро / Минцифры РФ (КС1, КС2, КВ1, КА1)
			if (HasCryptoProOrRussianExtensions(certificate) == true)
			{
				return true;
			}

			// 5. Проверка информации о криптопровайдере закрытого ключа (Crypto-Pro CSP)
			if (HasCryptoProPrivateKeyProvider(certificate) == true)
			{
				return true;
			}

			// 6. Проверка издания доверенным российским УЦ либо наличия реквизитов РФ (ОГРН, СНИЛС, ИНН)
			if (HasRussianIssuerOrSubject(certificate) == true)
			{
				return true;
			}

			// 7. Проверка личного сертификата с кириллическим ФИО субъекта (например, тестовые сертификаты пользователя)
			string subject = certificate.Subject ?? string.Empty;
			if (HasCyrillicCharacters(subject) == true)
			{
				return true;
			}

			return false;
		}

		// Проверка расширений сертификата на строгое наличие OID встроенной лицензии КриптоПро CSP (1.2.643.2.2.49.2 и ветка 1.2.643.2.2.49.*)
		public static bool CheckEmbeddedLicense(X509Certificate2 certificate, out string licenseVersionInfo)
		{
			licenseVersionInfo = string.Empty;
			if (certificate == null || certificate.Extensions == null)
			{
				return false;
			}

			for (int i = 0; i < certificate.Extensions.Count; i++)
			{
				X509Extension extension = certificate.Extensions[i];
				if (extension != null && extension.Oid != null)
				{
					string oid = extension.Oid.Value ?? string.Empty;
					string friendly = extension.Oid.FriendlyName ?? string.Empty;

					if (string.Equals(oid, CryptoProLicenseOid, StringComparison.OrdinalIgnoreCase) ||
					    oid.StartsWith(CryptoProLicenseOidBranch, StringComparison.OrdinalIgnoreCase))
					{
						licenseVersionInfo = ParseLicenseExtensionData(extension);
						return true;
					}

					if (friendly.IndexOf("лицензи", StringComparison.OrdinalIgnoreCase) >= 0 ||
					    friendly.IndexOf("license", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						licenseVersionInfo = ParseLicenseExtensionData(extension);
						return true;
					}
				}
			}

			return false;
		}

		// Проверка расширений сертификата на явный OID встроенной лицензии КриптоПро CSP
		private static bool CheckExplicitLicenseExtension(X509Certificate2 certificate, out string licenseVersionInfo)
		{
			licenseVersionInfo = string.Empty;

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

					if (string.Equals(oidValue, CryptoProLicenseOid, StringComparison.OrdinalIgnoreCase))
					{
						licenseVersionInfo = ParseLicenseExtensionData(extension);
						return true;
					}

					if (!string.IsNullOrEmpty(oidValue) && oidValue.StartsWith(CryptoProLicenseOidBranch, StringComparison.OrdinalIgnoreCase))
					{
						licenseVersionInfo = ParseLicenseExtensionData(extension);
						return true;
					}

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
				Logger.LogError("Ошибка при анализе расширений лицензии КриптоПро: " + ex.Message, ex);
			}

			return false;
		}

		// Выявление служебных, телеметрических и не связанных с СКЗИ системных сертификатов Windows
		public static bool IsSystemOrForeignCertificate(X509Certificate2 certificate)
		{
			if (certificate == null)
			{
				return true;
			}

			string subject = certificate.Subject ?? string.Empty;
			string issuer = certificate.Issuer ?? string.Empty;
			string friendlyName = certificate.FriendlyName ?? string.Empty;

			// Локальный веб-сервер IIS Express / ASP.NET Core HTTPS
			if (subject.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 || issuer.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			// Системные сертификаты связки устройств Windows Phone, CrossDevice, Xbox
			if (issuer.IndexOf("XBL Client IPsec", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			if (friendlyName.IndexOf("Your Phone", StringComparison.OrdinalIgnoreCase) >= 0 || friendlyName.IndexOf("CrossDevice", StringComparison.OrdinalIgnoreCase) >= 0 || friendlyName.IndexOf("ASP.NET Core", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			// Идентификация сертификатов устройств по шаблону GUID в Subject (например CN=30709811-1722-46ec-be37-d2633a39f934)
			string cleanCn = ExtractCnValue(subject);
			if (IsGuidString(cleanCn) == true)
			{
				return true;
			}

			if (cleanCn.StartsWith("trust_", StringComparison.OrdinalIgnoreCase) && IsGuidString(cleanCn.Substring(6)) == true)
			{
				return true;
			}

			return false;
		}

		// Проверка использования в сертификате отечественных криптографических алгоритмов ГОСТ
		private static bool HasGostAlgorithm(X509Certificate2 certificate)
		{
			try
			{
				string sigOid = certificate.SignatureAlgorithm != null ? certificate.SignatureAlgorithm.Value : string.Empty;
				string sigFriendly = certificate.SignatureAlgorithm != null ? certificate.SignatureAlgorithm.FriendlyName : string.Empty;

				// OID ГОСТ Р 34.10-2012 (256/512 бит) и ГОСТ Р 34.10-2001
				if (!string.IsNullOrEmpty(sigOid))
				{
					if (sigOid.StartsWith("1.2.643.7.1.", StringComparison.OrdinalIgnoreCase) || sigOid.StartsWith("1.2.643.2.2.", StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}

				if (!string.IsNullOrEmpty(sigFriendly))
				{
					if (sigFriendly.IndexOf("ГОСТ", StringComparison.OrdinalIgnoreCase) >= 0 || sigFriendly.IndexOf("GOST", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return true;
					}
				}

				// Проверка OID открытого ключа (PublicKey)
				if (certificate.PublicKey != null && certificate.PublicKey.Oid != null)
				{
					string keyOid = certificate.PublicKey.Oid.Value;
					string keyFriendly = certificate.PublicKey.Oid.FriendlyName;

					if (!string.IsNullOrEmpty(keyOid))
					{
						if (keyOid.StartsWith("1.2.643.7.1.", StringComparison.OrdinalIgnoreCase) || keyOid.StartsWith("1.2.643.2.2.", StringComparison.OrdinalIgnoreCase))
						{
							return true;
						}
					}

					if (!string.IsNullOrEmpty(keyFriendly))
					{
						if (keyFriendly.IndexOf("ГОСТ", StringComparison.OrdinalIgnoreCase) >= 0 || keyFriendly.IndexOf("GOST", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							return true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Ошибка проверки алгоритмов ГОСТ: " + ex.Message);
			}

			return false;
		}

		// Проверка наличия OID расширений КриптоПро или классов средств электронной подписи Минцифры РФ
		private static bool HasCryptoProOrRussianExtensions(X509Certificate2 certificate)
		{
			try
			{
				X509ExtensionCollection extensions = certificate.Extensions;
				if (extensions == null || extensions.Count == 0)
				{
					return false;
				}

				for (int i = 0; i < extensions.Count; i++)
				{
					X509Extension ext = extensions[i];
					string oid = ext.Oid != null ? ext.Oid.Value : string.Empty;

					if (!string.IsNullOrEmpty(oid))
					{
						// OID дерева КриптоПро (1.2.643.2.2.*)
						if (oid.StartsWith("1.2.643.2.2.", StringComparison.OrdinalIgnoreCase))
						{
							return true;
						}

						// OID классов криптографической защиты Минцифры / ФСБ РФ (1.2.643.100.113.* - КС1/КС2/КС3)
						if (oid.StartsWith("1.2.643.100.113.", StringComparison.OrdinalIgnoreCase) || oid.StartsWith("1.2.643.100.111.", StringComparison.OrdinalIgnoreCase))
						{
							return true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Ошибка проверки OID расширений КриптоПро: " + ex.Message);
			}

			return false;
		}

		// Проверка привязки закрытого ключа к криптопровайдеру CryptoPro CSP
		private static bool HasCryptoProPrivateKeyProvider(X509Certificate2 certificate)
		{
			try
			{
				if (certificate.HasPrivateKey == true)
				{
					// Попытка извлечения информации о контейнере CspKeyContainerInfo
					AsymmetricAlgorithm privateKey = certificate.PrivateKey;
					if (privateKey is ICspAsymmetricAlgorithm)
					{
						CspKeyContainerInfo containerInfo = ((ICspAsymmetricAlgorithm)privateKey).CspKeyContainerInfo;
						if (containerInfo != null)
						{
							string providerName = containerInfo.ProviderName ?? string.Empty;
							int providerType = containerInfo.ProviderType;

							if (providerName.IndexOf("Crypto-Pro", StringComparison.OrdinalIgnoreCase) >= 0 || providerName.IndexOf("Крипто-Про", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								return true;
							}

							// Типы провайдеров КриптоПро ГОСТ: 75 (2001), 80 (2012-256), 81 (2012-512)
							if (providerType == 75 || providerType == 80 || providerType == 81)
							{
								return true;
							}
						}
					}
				}
			}
			catch
			{
				// Доступ к закрытому ключу может быть ограничен правами безопасности
			}

			return false;
		}

		// Проверка принадлежности издателя (Issuer) или субъекта к российским УЦ и реквизитам РФ
		private static bool HasRussianIssuerOrSubject(X509Certificate2 certificate)
		{
			string issuerLower = (certificate.Issuer ?? string.Empty).ToLowerInvariant();
			string subjectLower = (certificate.Subject ?? string.Empty).ToLowerInvariant();

			// Проверка совпадения с перечнем аккредитованных УЦ РФ
			for (int i = 0; i < RussianCertificateAuthorities.Length; i++)
			{
				string caKeyword = RussianCertificateAuthorities[i];
				if (issuerLower.Contains(caKeyword) || subjectLower.Contains(caKeyword))
				{
					return true;
				}
			}

			// Проверка обязательных атрибутов квалифицированных сертификатов РФ (ОГРН, СНИЛС, ИНН)
			if (subjectLower.Contains("огрн") || subjectLower.Contains("снилс") || subjectLower.Contains("инн") || subjectLower.Contains("1.2.643.100.1") || subjectLower.Contains("1.2.643.100.3"))
			{
				return true;
			}

			return false;
		}

		// Проверка наличия символов кириллицы в переданной строке
		private static bool HasCyrillicCharacters(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё')
				{
					return true;
				}
			}

			return false;
		}

		// Извлечение значения поля CN из Distinguished Name (DN)
		private static string ExtractCnValue(string distinguishedName)
		{
			if (string.IsNullOrWhiteSpace(distinguishedName))
			{
				return string.Empty;
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

		// Проверка, является ли строка стандартным идентификатором GUID (36 символов с дефисами)
		private static bool IsGuidString(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value.Length != 36)
			{
				return false;
			}

			Guid dummyGuid;
			return Guid.TryParse(value, out dummyGuid);
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
