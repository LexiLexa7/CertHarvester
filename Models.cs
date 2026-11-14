using System;
using System.Collections.Generic;

namespace CertHarvester
{
	/// <summary>
	/// Модель записи сертификата со всеми обязательными полями для сохранения в базу данных MSSQL
	/// </summary>
	public class CertificateRecord
	{
		/// <summary>
		/// Серийный номер сертификата (уникальный идентификатор)
		/// </summary>
		public string SerialNumber { get; set; }

		/// <summary>
		/// Дата и время внесения записи в таблицу БД
		/// </summary>
		public DateTime DateInserted { get; set; }

		/// <summary>
		/// Дата и время обновления данных в таблице БД
		/// </summary>
		public DateTime DateUpdated { get; set; }

		/// <summary>
		/// Имя владельца сертификата (Subject / CN)
		/// </summary>
		public string OwnerName { get; set; }

		/// <summary>
		/// Название удостоверяющего центра (Issuer / CA)
		/// </summary>
		public string Issuer { get; set; }

		/// <summary>
		/// Версия установленной или поддерживаемой библиотеки КриптоПро CSP
		/// </summary>
		public string CryptoProVersion { get; set; }

		/// <summary>
		/// Признак наличия встроенной лицензии КриптоПро в сертификате
		/// </summary>
		public bool HasEmbeddedLicense { get; set; }

		/// <summary>
		/// Исходный путь к файлу сертификата на диске или сетевом ресурсе
		/// </summary>
		public string FilePath { get; set; }

		/// <summary>
		/// Имя компьютера, на котором обнаружен сертификат
		/// </summary>
		public string ComputerName { get; set; }

		// Конструктор по умолчанию для инициализации полей
		public CertificateRecord()
		{
			SerialNumber = string.Empty;
			DateInserted = DateTime.Now;
			DateUpdated = DateTime.Now;
			OwnerName = string.Empty;
			Issuer = string.Empty;
			CryptoProVersion = string.Empty;
			HasEmbeddedLicense = false;
			FilePath = string.Empty;
			ComputerName = Environment.MachineName;
		}

		// Параметризованный конструктор для создания полной записи сертификата
		public CertificateRecord(string serialNumber, string ownerName, string issuer, string cryptoProVersion, bool hasEmbeddedLicense, string filePath, string computerName)
		{
			SerialNumber = serialNumber;
			DateTime currentTimestamp = DateTime.Now;
			DateInserted = currentTimestamp;
			DateUpdated = currentTimestamp;
			OwnerName = ownerName;
			Issuer = issuer;
			CryptoProVersion = cryptoProVersion;
			HasEmbeddedLicense = hasEmbeddedLicense;
			FilePath = filePath;
			ComputerName = computerName;
		}

		// Форматирование строкового представления объекта сертификата для вывода в консоль
		public override string ToString()
		{
			string licenseStatus = HasEmbeddedLicense ? "Да" : "Нет";
			return string.Format("Сертификат: {0} | Владелец: {1} | УЦ: {2} | КриптоПро: {3} | Лицензия: {4}", SerialNumber, OwnerName, Issuer, CryptoProVersion, licenseStatus);
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
			IsDarkTheme = false;
			Certificates = new List<CertificateRecord>();
		}
	}
}
