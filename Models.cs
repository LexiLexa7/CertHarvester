using System;
using System.Collections.Generic;

namespace CertHarvester
{
	/// <summary>
	/// Модель привязки сертификата к конкретному компьютеру сети (таблица CertificateLocations)
	/// </summary>
	public class CertificateLocationRecord
	{
		/// <summary>
		/// Серийный номер сертификата (часть составного первичного ключа)
		/// </summary>
		public string SerialNumber { get; set; }

		/// <summary>
		/// Имя сетевого компьютера (часть составного первичного ключа)
		/// </summary>
		public string MachineName { get; set; }

		/// <summary>
		/// IP-адрес компьютера
		/// </summary>
		public string MachineIp { get; set; }

		/// <summary>
		/// Источник или путь получения сертификата (хранилище, реестр, контейнер КриптоПро или путь к файлу)
		/// </summary>
		public string SourcePath { get; set; }

		/// <summary>
		/// Дата и время последнего обнаружения сертификата на данном компьютере
		/// </summary>
		public DateTime LastSeenAt { get; set; }

		// Конструктор по умолчанию для инициализации полей
		public CertificateLocationRecord()
		{
			SerialNumber = string.Empty;
			MachineName = string.Empty;
			MachineIp = string.Empty;
			SourcePath = string.Empty;
			LastSeenAt = DateTime.Now;
		}

		// Параметризованный конструктор для создания записи привязки сертификата к ПК
		public CertificateLocationRecord(string serialNumber, string machineName, string machineIp, string sourcePath, DateTime? lastSeenAt = null)
		{
			SerialNumber = serialNumber ?? string.Empty;
			MachineName = machineName ?? string.Empty;
			MachineIp = machineIp ?? string.Empty;
			SourcePath = sourcePath ?? string.Empty;
			LastSeenAt = lastSeenAt ?? DateTime.Now;
		}
	}

	/// <summary>
	/// Модель записи сертификата со всеми обязательными полями для сохранения в базу данных MSSQL
	/// </summary>
	public class CertificateRecord
	{
		/// <summary>
		/// Серийный номер сертификата (уникальный идентификатор и первичный ключ по ТЗ)
		/// </summary>
		public string SerialNumber { get; set; }

		/// <summary>
		/// Дата и время внесения записи в таблицу БД (CreatedAt по ТЗ)
		/// </summary>
		public DateTime CreatedAt { get; set; }

		/// <summary>
		/// Дата и время обновления данных в таблице БД (UpdatedAt по ТЗ)
		/// </summary>
		public DateTime UpdatedAt { get; set; }

		/// <summary>
		/// Имя владельца сертификата (OwnerName по ТЗ)
		/// </summary>
		public string OwnerName { get; set; }

		/// <summary>
		/// Название удостоверяющего центра (IssuerName по ТЗ)
		/// </summary>
		public string IssuerName { get; set; }

		/// <summary>
		/// Версия установленной или поддерживаемой библиотеки КриптоПро CSP
		/// </summary>
		public string CryptoProVersion { get; set; }

		/// <summary>
		/// Признак наличия встроенной лицензии КриптоПро в сертификате
		/// </summary>
		public bool HasEmbeddedLicense { get; set; }

		/// <summary>
		/// Имя компьютера, на котором обнаружен сертификат
		/// </summary>
		public string MachineName { get; set; }

		/// <summary>
		/// IP-адрес компьютера
		/// </summary>
		public string MachineIp { get; set; }

		/// <summary>
		/// Исходный путь или описание источника сертификата
		/// </summary>
		public string SourcePath { get; set; }

		/// <summary>
		/// Список всех компьютеров сети, на которых обнаружен данный сертификат
		/// </summary>
		public List<CertificateLocationRecord> Locations { get; set; }

		// Свойства для обратной совместимости с существующим кодом
		public DateTime DateInserted { get { return CreatedAt; } set { CreatedAt = value; } }
		public DateTime DateUpdated { get { return UpdatedAt; } set { UpdatedAt = value; } }
		public string Issuer { get { return IssuerName; } set { IssuerName = value; } }
		public string FilePath { get { return SourcePath; } set { SourcePath = value; } }
		public string ComputerName { get { return MachineName; } set { MachineName = value; } }

		// Конструктор по умолчанию для инициализации полей
		public CertificateRecord()
		{
			SerialNumber = string.Empty;
			DateTime currentTimestamp = DateTime.Now;
			CreatedAt = currentTimestamp;
			UpdatedAt = currentTimestamp;
			OwnerName = string.Empty;
			IssuerName = string.Empty;
			CryptoProVersion = "—";
			HasEmbeddedLicense = false;
			MachineName = Environment.MachineName;
			MachineIp = "127.0.0.1";
			SourcePath = string.Empty;
			Locations = new List<CertificateLocationRecord>();
		}

		// Параметризованный конструктор для создания полной записи сертификата
		public CertificateRecord(string serialNumber, string ownerName, string issuerName, string cryptoProVersion, bool hasEmbeddedLicense, string sourcePath, string machineName, string machineIp = "127.0.0.1")
		{
			SerialNumber = serialNumber ?? string.Empty;
			DateTime currentTimestamp = DateTime.Now;
			CreatedAt = currentTimestamp;
			UpdatedAt = currentTimestamp;
			OwnerName = ownerName ?? string.Empty;
			IssuerName = issuerName ?? string.Empty;
			CryptoProVersion = string.IsNullOrEmpty(cryptoProVersion) ? "—" : cryptoProVersion;
			HasEmbeddedLicense = hasEmbeddedLicense;
			SourcePath = sourcePath ?? string.Empty;
			MachineName = string.IsNullOrEmpty(machineName) ? Environment.MachineName : machineName;
			MachineIp = string.IsNullOrEmpty(machineIp) ? "127.0.0.1" : machineIp;
			Locations = new List<CertificateLocationRecord>();
			AddLocation(MachineName, MachineIp, SourcePath, currentTimestamp);
		}

		// Добавление или обновление расположения сертификата на конкретном компьютере сети
		public void AddLocation(string machineName, string machineIp, string sourcePath, DateTime? lastSeenAt = null)
		{
			if (Locations == null)
			{
				Locations = new List<CertificateLocationRecord>();
			}

			if (string.IsNullOrWhiteSpace(machineName))
			{
				machineName = Environment.MachineName;
			}

			DateTime seen = lastSeenAt ?? DateTime.Now;
			for (int i = 0; i < Locations.Count; i++)
			{
				if (string.Equals(Locations[i].MachineName, machineName, StringComparison.OrdinalIgnoreCase))
				{
					Locations[i].LastSeenAt = seen;
					if (!string.IsNullOrWhiteSpace(sourcePath))
					{
						Locations[i].SourcePath = sourcePath;
					}
					if (!string.IsNullOrWhiteSpace(machineIp))
					{
						Locations[i].MachineIp = machineIp;
					}
					return;
				}
			}

			Locations.Add(new CertificateLocationRecord(SerialNumber, machineName, machineIp, sourcePath, seen));
		}

		// Проверка обнаружения сертификата на указанном компьютере сети
		public bool IsFoundOnMachine(string targetMachine)
		{
			if (string.IsNullOrWhiteSpace(targetMachine))
			{
				return true;
			}

			if (string.Equals(MachineName, targetMachine, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(MachineIp, targetMachine, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (Locations != null)
			{
				for (int i = 0; i < Locations.Count; i++)
				{
					if (string.Equals(Locations[i].MachineName, targetMachine, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(Locations[i].MachineIp, targetMachine, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}

		// Форматирование строкового представления объекта сертификата для вывода в консоль
		public override string ToString()
		{
			string licenseStatus = HasEmbeddedLicense ? "Да" : "Нет";
			return string.Format("Сертификат: {0} | Владелец: {1} | УЦ: {2} | КриптоПро: {3} | Лицензия: {4} | ПК: {5}", SerialNumber, OwnerName, IssuerName, CryptoProVersion, licenseStatus, MachineName);
		}
	}

	/// <summary>
	/// Модель учетных данных для авторизованного сетевого подключения к удаленному компьютеру (net use)
	/// </summary>
	public class NetworkComputerCredential
	{
		/// <summary>
		/// Имя или IP-адрес сетевого компьютера
		/// </summary>
		public string ComputerAddress { get; set; }

		/// <summary>
		/// Имя пользователя (логин) для подключения
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		/// Пароль учетной записи пользователя
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// Признак активного подключения по сетевой сессии net use
		/// </summary>
		public bool IsConnected { get; set; }

		/// <summary>
		/// Список дополнительных директорий на данном компьютере для поиска сертификатов
		/// </summary>
		public List<string> CustomDirectories { get; set; }

		// Конструктор по умолчанию для инициализации полей
		public NetworkComputerCredential()
		{
			ComputerAddress = string.Empty;
			Username = string.Empty;
			Password = string.Empty;
			IsConnected = false;
			CustomDirectories = new List<string>();
		}

		// Параметризованный конструктор для создания записи учетных данных
		public NetworkComputerCredential(string computerAddress, string username, string password)
		{
			ComputerAddress = computerAddress;
			Username = username;
			Password = password;
			IsConnected = false;
			CustomDirectories = new List<string>();
		}
	}

	/// <summary>
	/// Модель конфигурационных параметров приложения CertHarvester
	/// </summary>
	public class AppConfig
	{
		/// <summary>
		/// Список локальных и сетевых директорий для сканирования сертификатов
		/// </summary>
		public List<string> ScanDirectories { get; set; }

		/// <summary>
		/// Список имен или IP-адресов ПК в локальной сети для сетевого сканирования
		/// </summary>
		public List<string> NetworkComputers { get; set; }

		/// <summary>
		/// Список сохраненных учетных данных для подключения к компьютерам сети (net use)
		/// </summary>
		public List<NetworkComputerCredential> ComputerCredentials { get; set; }

		/// <summary>
		/// Строка подключения к базе данных Microsoft SQL Server 2019
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// Имя таблицы в БД для хранения сведений о сертификатах
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// Автоматическое создание таблицы в БД при ее отсутствии
		/// </summary>
		public bool AutoCreateTable { get; set; }

		/// <summary>
		/// Признак использования темной темы графического интерфейса
		/// </summary>
		public bool IsDarkTheme { get; set; }

		/// <summary>
		/// Список сохраненных сертификатов для постоянного хранения в конфигурации
		/// </summary>
		public List<CertificateRecord> Certificates { get; set; }

		// Конструктор по умолчанию с заполнением типовыми начальными значениями
		public AppConfig()
		{
			ScanDirectories = new List<string>();
			NetworkComputers = new List<string>();
			ComputerCredentials = new List<NetworkComputerCredential>();
			ConnectionString = "Server=localhost;Database=CertHarvesterDB;Integrated Security=True;";
			TableName = "Certificates";
			AutoCreateTable = true;
			IsDarkTheme = true;
			Certificates = new List<CertificateRecord>();
		}
	}
}
