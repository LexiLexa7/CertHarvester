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

		// Сохранение переданного объекта конфигурации в JSON-файл
		public static bool SaveConfig(AppConfig config, string filePath = null)
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
				Logger.LogInfo("Конфигурация успешно сохранена в файл: " + targetFilePath);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при сохранении конфигурации в " + targetFilePath + ": " + ex.Message, ex);
				return false;
			}
		}

		// Формирование начального объекта конфигурации с типовыми директориями и параметрами для локального запуска
		public static AppConfig CreateDefaultConfig()
		{
			AppConfig config = new AppConfig();
			config.ScanDirectories.Add(@"C:\Certificates\Personal");
			config.ScanDirectories.Add(@"D:\CryptoPro\Certs");
			config.ScanDirectories.Add(@"C:\Users\Public\Certs");
			config.NetworkComputers.Add("PC-BUH-01 (192.168.10.11)");
			config.NetworkComputers.Add("PC-BUH-02 (192.168.10.12)");
			config.NetworkComputers.Add("PC-HR-03 (192.168.10.23)");
			config.NetworkComputers.Add("PC-LAW-04 (192.168.10.31)");
			config.NetworkComputers.Add("PC-DIR-06 (192.168.10.52)");
			config.ConnectionString = "Server=SQL-SRV\\MSSQL2019;Database=CryptoInventory;Integrated Security=True;";
			config.TableName = "Certificates";
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
