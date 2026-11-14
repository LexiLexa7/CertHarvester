using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CertHarvester
{
	/// <summary>
	/// Класс управления чтением, сохранением и инициализацией параметров конфигурации приложения
	/// </summary>
	public static class ConfigManager
	{
		private const string DefaultConfigFileName = "config.json";

		// Загрузка конфигурации из JSON-файла или создание файла конфигурации со значениями по умолчанию
		public static AppConfig LoadConfig(string filePath = null)
		{
			string targetFilePath = ResolveConfigPath(filePath);

			try
			{
				if (File.Exists(targetFilePath))
				{
					string jsonContent = File.ReadAllText(targetFilePath);
					JavaScriptSerializer serializer = new JavaScriptSerializer();
					AppConfig loadedConfig = serializer.Deserialize<AppConfig>(jsonContent);

					if (loadedConfig != null)
					{
						Logger.LogInfo("Конфигурация успешно загружена из файла: " + targetFilePath);
						EnsureConfigCollections(loadedConfig);
						return loadedConfig;
					}
				}
				else
				{
					Logger.LogWarning("Файл конфигурации не найден, создается конфигурация по умолчанию: " + targetFilePath);
					AppConfig defaultConfig = CreateDefaultConfig();
					SaveConfig(defaultConfig, targetFilePath);
					return defaultConfig;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Сбой при загрузке конфигурации из " + targetFilePath + ": " + ex.Message, ex);
			}

			return CreateDefaultConfig();
		}

		// Сохранение переданного объекта конфигурации в JSON-файл с возможностью тихого режима без логирования
		public static bool SaveConfig(AppConfig config, string filePath = null, bool silent = false)
		{
			if (config == null)
			{
				Logger.LogError("Попытка сохранения пустой конфигурации (null)");
				return false;
			}

			string targetFilePath = ResolveConfigPath(filePath);

			try
			{
				EnsureConfigCollections(config);
				JavaScriptSerializer serializer = new JavaScriptSerializer();
				string jsonContent = serializer.Serialize(config);

				string targetDirectory = Path.GetDirectoryName(targetFilePath);
				if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
				{
					Directory.CreateDirectory(targetDirectory);
				}

				File.WriteAllText(targetFilePath, jsonContent);

				if (silent == false)
				{
					Logger.LogInfo("Конфигурация успешно сохранена в файл: " + targetFilePath);
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при сохранении конфигурации в " + targetFilePath + ": " + ex.Message, ex);
				return false;
			}
		}

		// Перегрузка сохранения конфигурации с флагом тихого режима
		public static bool SaveConfig(AppConfig config, bool silent)
		{
			return SaveConfig(config, null, silent);
		}

		// Формирование начального объекта конфигурации из файла или создание чистого конфига
		public static AppConfig CreateDefaultConfig()
		{
			AppConfig config = new AppConfig();
			config.ScanDirectories.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates"));
			config.NetworkComputers.Add(Environment.MachineName);
			config.ConnectionString = "Server=localhost;Database=CertHarvesterDB;Integrated Security=True;";
			config.TableName = "Certificates";
			config.AutoCreateTable = true;
			config.IsDarkTheme = false;
			config.Certificates = new List<CertificateRecord>();
			return config;
		}

		// Проверка инициализации коллекций внутри объекта конфигурации во избежание NullReferenceException
		private static void EnsureConfigCollections(AppConfig config)
		{
			if (config.ScanDirectories == null)
			{
				config.ScanDirectories = new List<string>();
			}

			if (config.NetworkComputers == null)
			{
				config.NetworkComputers = new List<string>();
			}

			if (config.ComputerCredentials == null)
			{
				config.ComputerCredentials = new List<NetworkComputerCredential>();
			}

			if (config.Certificates == null)
			{
				config.Certificates = new List<CertificateRecord>();
			}
		}

		// Определение абсолютного пути к файлу конфигурации относительно рабочей директории
		private static string ResolveConfigPath(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigFileName);
			}

			if (Path.IsPathRooted(filePath))
			{
				return filePath;
			}

			return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
		}
	}
}
