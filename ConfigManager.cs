using System;
using System.IO;
using System.Web.Script.Serialization;

namespace CertHarvester
{
	/// <summary>
	/// Класс управления чтением и сохранением параметров конфигурации приложения (Зона ответственности: Участник 3)
	/// </summary>
	public static class ConfigManager
	{
		private const string DefaultConfigFileName = "config.json";

		// Загрузка конфигурации из файла или создание файла конфигурации по умолчанию
		public static AppConfig LoadConfig(string filePath = null)
		{
			// TODO (Участник 3): Реализовать валидацию корректности путей сканирования при загрузке
			// TODO (Участник 3): Добавить безопасное хранение учетных данных строки подключения к MSSQL
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
			// TODO (Участник 3): Добавить резервное копирование предыдущей версии конфига перед перезаписью (.bak)
			// TODO (Участник 3): Реализовать проверку прав на запись в целевой каталог
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

		// Формирование начального объекта конфигурации с типовыми путями для локального тестирования
		public static AppConfig CreateDefaultConfig()
		{
			// TODO (Участник 3): Загружать типовые пути сканирования из переменных окружения или системных каталогов
			AppConfig config = new AppConfig();
			config.ScanDirectories.Add(@"C:\Certs");
			config.ScanDirectories.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates"));
			config.NetworkComputers.Add(Environment.MachineName);
			config.ConnectionString = "Server=localhost;Database=CertHarvesterDB;Integrated Security=True;";
			config.TableName = "Certificates";
			return config;
		}

		// Проверка инициализации списков внутри объекта конфигурации
		private static void EnsureConfigCollections(AppConfig config)
		{
			if (config.ScanDirectories == null)
			{
				config.ScanDirectories = new System.Collections.Generic.List<string>();
			}

			if (config.NetworkComputers == null)
			{
				config.NetworkComputers = new System.Collections.Generic.List<string>();
			}
		}

		// Определение полного пути к файлу конфигурации относительно каталога приложения
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
