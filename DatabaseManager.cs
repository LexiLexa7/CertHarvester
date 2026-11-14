using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace CertHarvester
{
	/// <summary>
	/// Класс управления взаимодействием с базой данных Microsoft SQL Server 2019
	/// Реализует нормализованную архитектуру хранения с двумя связанными таблицами:
	/// 1) Основная таблица (строго 7 колонок по ТЗ): Certificates
	/// 2) Вспомогательная таблица привязки к компьютерам: CertificateLocations
	/// </summary>
	public static class DatabaseManager
	{
		// Проверка доступности подключения к серверу базы данных MSSQL
		public static bool TestConnection(string connectionString)
		{
			try
			{
				EnsureDatabaseExists(connectionString);

				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					return connection.State == ConnectionState.Open;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Сбой проверки соединения с базой данных MSSQL: " + ex.Message, ex);
				return false;
			}
		}

		// Полная очистка обеих связанных таблиц в базе данных MSSQL (CertificateLocations и основной таблицы)
		public static bool ClearTables(string connectionString, string tableName = "Certificates")
		{
			try
			{
				if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(tableName))
				{
					return false;
				}

				EnsureDatabaseExists(connectionString);

				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					EnsureTablesExist(connection, tableName);

					// Сначала удаляем записи из дочерней таблицы CertificateLocations
					string deleteLocationsQuery = @"
						IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CertificateLocations')
						BEGIN
							DELETE FROM [dbo].[CertificateLocations];
						END";

					using (SqlCommand cmd = new SqlCommand(deleteLocationsQuery, connection))
					{
						cmd.ExecuteNonQuery();
					}

					// Затем очищаем основную таблицу сертификатов
					string deleteMainQuery = "DELETE FROM [dbo].[" + tableName + "]";
					using (SqlCommand cmd = new SqlCommand(deleteMainQuery, connection))
					{
						cmd.ExecuteNonQuery();
					}

					Logger.LogInfo("Таблицы '" + tableName + "' и 'CertificateLocations' в базе данных MSSQL успешно полностью очищены");
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при очистке таблиц в БД MSSQL: " + ex.Message, ex);
				return false;
			}
		}

		// Метод очистки для совместимости с существующим вызовом ClearTable
		public static bool ClearTable(string connectionString, string tableName = "Certificates")
		{
			return ClearTables(connectionString, tableName);
		}

		// Автоматическая проверка существования целевой базы данных на сервере MSSQL и ее создание при отсутствии
		public static void EnsureDatabaseExists(string connectionString)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(connectionString))
				{
					return;
				}

				SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
				string targetDatabaseName = builder.InitialCatalog;

				if (string.IsNullOrWhiteSpace(targetDatabaseName) || string.Equals(targetDatabaseName, "master", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}

				// Формирование строки подключения к системной базе master для выполнения административных команд
				builder.InitialCatalog = "master";
				string masterConnectionString = builder.ConnectionString;

				using (SqlConnection masterConnection = new SqlConnection(masterConnectionString))
				{
					masterConnection.Open();

					string checkDbQuery = "SELECT COUNT(*) FROM sys.databases WHERE [name] = @DatabaseName";
					using (SqlCommand checkCmd = new SqlCommand(checkDbQuery, masterConnection))
					{
						checkCmd.Parameters.Add(new SqlParameter("@DatabaseName", targetDatabaseName));
						int count = Convert.ToInt32(checkCmd.ExecuteScalar());

						if (count == 0)
						{
							Logger.LogInfo("База данных '" + targetDatabaseName + "' отсутствует на сервере MSSQL. Выполняется автоматическое создание");
							string createDbQuery = "CREATE DATABASE [" + targetDatabaseName + "]";
							using (SqlCommand createCmd = new SqlCommand(createDbQuery, masterConnection))
							{
								createCmd.ExecuteNonQuery();
								Logger.LogInfo("База данных '" + targetDatabaseName + "' успешно создана на SQL Server 2019");
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Не удалось автоматически проверить/создать БД через master (возможно, нет прав CREATE DATABASE): " + ex.Message);
			}
		}

		// Проверка существования и создание двух связанных таблиц (основной таблицы строго из 7 полей и таблицы локаций)
		public static void EnsureTablesExist(SqlConnection connection, string tableName)
		{
			if (string.IsNullOrWhiteSpace(tableName))
			{
				tableName = "Certificates";
			}

			// 1. Создание основной таблицы сертификатов (строго 7 столбцов по ТЗ)
			string createMainTableQuery = @"
				IF NOT EXISTS (
					SELECT * FROM INFORMATION_SCHEMA.TABLES 
					WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TargetTableName
				)
				BEGIN
					CREATE TABLE [dbo].[" + tableName + @"] (
						[SerialNumber] NVARCHAR(128) NOT NULL PRIMARY KEY,
						[CreatedAt] DATETIME NOT NULL,
						[UpdatedAt] DATETIME NOT NULL,
						[OwnerName] NVARCHAR(512) NULL,
						[IssuerName] NVARCHAR(512) NULL,
						[CryptoProVersion] NVARCHAR(64) NULL,
						[HasEmbeddedLicense] BIT NOT NULL
					);
				END
				ELSE
				BEGIN
					-- Автоматическая миграция колонок на случай, если таблица была создана со старыми именами
					IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TargetTableName AND COLUMN_NAME = 'DateInserted')
					BEGIN
						EXEC sp_rename '" + tableName + @".DateInserted', 'CreatedAt', 'COLUMN';
					END;
					IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TargetTableName AND COLUMN_NAME = 'DateUpdated')
					BEGIN
						EXEC sp_rename '" + tableName + @".DateUpdated', 'UpdatedAt', 'COLUMN';
					END;
					IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TargetTableName AND COLUMN_NAME = 'Issuer')
					BEGIN
						EXEC sp_rename '" + tableName + @".Issuer', 'IssuerName', 'COLUMN';
					END;
				END";

			using (SqlCommand command = new SqlCommand(createMainTableQuery, connection))
			{
				command.Parameters.Add(new SqlParameter("@TargetTableName", tableName));
				command.ExecuteNonQuery();
			}

			// 2. Создание вспомогательной связанной таблицы CertificateLocations с составным первичным ключом
			string createLocationsTableQuery = @"
				IF NOT EXISTS (
					SELECT * FROM INFORMATION_SCHEMA.TABLES 
					WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CertificateLocations'
				)
				BEGIN
					CREATE TABLE [dbo].[CertificateLocations] (
						[SerialNumber] NVARCHAR(128) NOT NULL,
						[MachineName] NVARCHAR(128) NOT NULL,
						[MachineIp] NVARCHAR(64) NULL,
						[SourcePath] NVARCHAR(512) NULL,
						[LastSeenAt] DATETIME NOT NULL,
						CONSTRAINT [PK_CertificateLocations] PRIMARY KEY ([SerialNumber], [MachineName])
					);
				END";

			using (SqlCommand command = new SqlCommand(createLocationsTableQuery, connection))
			{
				command.ExecuteNonQuery();
			}

			Logger.LogInfo("Проверка структуры таблиц '" + tableName + "' и 'CertificateLocations' успешно выполнена");
		}

		// Метод для обратной совместимости вызова EnsureTableExists
		public static void EnsureTableExists(SqlConnection connection, string tableName)
		{
			EnsureTablesExist(connection, tableName);
		}

		// Сохранение списка сертификатов в БД с нормализацией по 2 связанным таблицам
		public static int SaveCertificates(List<CertificateRecord> certificates, string connectionString, string tableName = "Certificates")
		{
			if (certificates == null || certificates.Count == 0)
			{
				Logger.LogInfo("Список сертификатов для записи в БД пуст");
				return 0;
			}

			int processedCount = 0;

			try
			{
				// 1. Проверка существования БД и создание при необходимости
				EnsureDatabaseExists(connectionString);

				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					Logger.LogInfo("Успешное подключение к СУБД MSSQL для синхронизации сертификатов");

					// 2. Обязательная проверка существования структуры двух связанных таблиц
					EnsureTablesExist(connection, tableName);

					// 3. Построчная обработка каждого сертификата с защитой от дублирования
					for (int i = 0; i < certificates.Count; i++)
					{
						CertificateRecord cert = certificates[i];
						bool isSaved = UpsertCertificateRecord(connection, cert, tableName);
						if (isSaved == true)
						{
							processedCount = processedCount + 1;
						}
					}
				}

				Logger.LogInfo("Синхронизация с БД MSSQL успешно завершена. Обработано записей: " + processedCount);
			}
			catch (SqlException sqlEx)
			{
				Logger.LogError("Сбой SQL Server при сохранении сертификатов: " + sqlEx.Message, sqlEx);
			}
			catch (Exception ex)
			{
				Logger.LogError("Непредвиденная ошибка в модуле DatabaseManager: " + ex.Message, ex);
			}

			return processedCount;
		}

		// Вставка или обновление сертификата в Certificates (строго 7 колонок) и привязки к ПК в CertificateLocations
		private static bool UpsertCertificateRecord(SqlConnection connection, CertificateRecord record, string tableName)
		{
			try
			{
				if (record == null || string.IsNullOrWhiteSpace(record.SerialNumber))
				{
					return false;
				}

				DateTime currentTimestamp = DateTime.Now;

				// 1. Проверка наличия записи с данным серийным номером в основной таблице Certificates
				string selectQuery = "SELECT COUNT(*) FROM [dbo].[" + tableName + "] WHERE [SerialNumber] = @SerialNumber";
				bool recordExists = false;

				using (SqlCommand checkCmd = new SqlCommand(selectQuery, connection))
				{
					checkCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
					int count = Convert.ToInt32(checkCmd.ExecuteScalar());
					recordExists = count > 0;
				}

				string issuer = !string.IsNullOrEmpty(record.IssuerName) ? record.IssuerName : record.Issuer;

				if (recordExists == true)
				{
					// UPDATE: обновляем UpdatedAt и остальные поля без дубликатов
					string updateQuery = @"
						UPDATE [dbo].[" + tableName + @"] 
						SET [UpdatedAt] = @UpdatedAt,
						    [OwnerName] = @OwnerName,
						    [IssuerName] = @IssuerName,
						    [CryptoProVersion] = @CryptoProVersion,
						    [HasEmbeddedLicense] = @HasEmbeddedLicense
						WHERE [SerialNumber] = @SerialNumber";

					using (SqlCommand updateCmd = new SqlCommand(updateQuery, connection))
					{
						updateCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
						updateCmd.Parameters.Add(new SqlParameter("@UpdatedAt", currentTimestamp));
						updateCmd.Parameters.Add(new SqlParameter("@OwnerName", (object)record.OwnerName ?? DBNull.Value));
						updateCmd.Parameters.Add(new SqlParameter("@IssuerName", (object)issuer ?? DBNull.Value));
						updateCmd.Parameters.Add(new SqlParameter("@CryptoProVersion", (object)record.CryptoProVersion ?? DBNull.Value));
						updateCmd.Parameters.Add(new SqlParameter("@HasEmbeddedLicense", record.HasEmbeddedLicense));

						updateCmd.ExecuteNonQuery();
					}

					Logger.LogInfo("Обновлен существующий сертификат в основной таблице БД: " + record.SerialNumber);
				}
				else
				{
					// INSERT: вставляем новую запись (строго 7 столбцов по ТЗ)
					string insertQuery = @"
						INSERT INTO [dbo].[" + tableName + @"] 
						([SerialNumber], [CreatedAt], [UpdatedAt], [OwnerName], [IssuerName], [CryptoProVersion], [HasEmbeddedLicense])
						VALUES 
						(@SerialNumber, @CreatedAt, @UpdatedAt, @OwnerName, @IssuerName, @CryptoProVersion, @HasEmbeddedLicense)";

					DateTime createdAt = record.CreatedAt != default(DateTime) ? record.CreatedAt : currentTimestamp;

					using (SqlCommand insertCmd = new SqlCommand(insertQuery, connection))
					{
						insertCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
						insertCmd.Parameters.Add(new SqlParameter("@CreatedAt", createdAt));
						insertCmd.Parameters.Add(new SqlParameter("@UpdatedAt", currentTimestamp));
						insertCmd.Parameters.Add(new SqlParameter("@OwnerName", (object)record.OwnerName ?? DBNull.Value));
						insertCmd.Parameters.Add(new SqlParameter("@IssuerName", (object)issuer ?? DBNull.Value));
						insertCmd.Parameters.Add(new SqlParameter("@CryptoProVersion", (object)record.CryptoProVersion ?? DBNull.Value));
						insertCmd.Parameters.Add(new SqlParameter("@HasEmbeddedLicense", record.HasEmbeddedLicense));

						insertCmd.ExecuteNonQuery();
					}

					Logger.LogInfo("Внесен новый уникальный сертификат в основную таблицу БД: " + record.SerialNumber);
				}

				// 2. Запись и обновление сведений в таблице CertificateLocations для каждого компьютера
				List<CertificateLocationRecord> locations = record.Locations;
				if (locations == null || locations.Count == 0)
				{
					locations = new List<CertificateLocationRecord>
					{
						new CertificateLocationRecord(record.SerialNumber, record.MachineName, record.MachineIp, record.SourcePath, currentTimestamp)
					};
				}

				for (int locIdx = 0; locIdx < locations.Count; locIdx++)
				{
					CertificateLocationRecord loc = locations[locIdx];
					string machineName = !string.IsNullOrWhiteSpace(loc.MachineName) ? loc.MachineName : Environment.MachineName;
					string machineIp = !string.IsNullOrWhiteSpace(loc.MachineIp) ? loc.MachineIp : "127.0.0.1";
					string sourcePath = !string.IsNullOrWhiteSpace(loc.SourcePath) ? loc.SourcePath : (record.SourcePath ?? string.Empty);
					DateTime lastSeen = loc.LastSeenAt != default(DateTime) ? loc.LastSeenAt : currentTimestamp;

					string checkLocQuery = "SELECT COUNT(*) FROM [dbo].[CertificateLocations] WHERE [SerialNumber] = @SerialNumber AND [MachineName] = @MachineName";
					bool locExists = false;

					using (SqlCommand checkLocCmd = new SqlCommand(checkLocQuery, connection))
					{
						checkLocCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
						checkLocCmd.Parameters.Add(new SqlParameter("@MachineName", machineName));
						int locCount = Convert.ToInt32(checkLocCmd.ExecuteScalar());
						locExists = locCount > 0;
					}

					if (locExists == true)
					{
						// Если пара SerialNumber и MachineName уже существует, обновляем LastSeenAt, SourcePath и MachineIp
						string updateLocQuery = @"
							UPDATE [dbo].[CertificateLocations]
							SET [MachineIp] = @MachineIp,
								[SourcePath] = @SourcePath,
								[LastSeenAt] = @LastSeenAt
							WHERE [SerialNumber] = @SerialNumber AND [MachineName] = @MachineName";

						using (SqlCommand updateLocCmd = new SqlCommand(updateLocQuery, connection))
						{
							updateLocCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
							updateLocCmd.Parameters.Add(new SqlParameter("@MachineName", machineName));
							updateLocCmd.Parameters.Add(new SqlParameter("@MachineIp", (object)machineIp ?? DBNull.Value));
							updateLocCmd.Parameters.Add(new SqlParameter("@SourcePath", (object)sourcePath ?? DBNull.Value));
							updateLocCmd.Parameters.Add(new SqlParameter("@LastSeenAt", lastSeen));

							updateLocCmd.ExecuteNonQuery();
						}
					}
					else
					{
						// Иначе выполняем INSERT новой привязки сертификата к компьютеру
						string insertLocQuery = @"
							INSERT INTO [dbo].[CertificateLocations]
							([SerialNumber], [MachineName], [MachineIp], [SourcePath], [LastSeenAt])
							VALUES
							(@SerialNumber, @MachineName, @MachineIp, @SourcePath, @LastSeenAt)";

						using (SqlCommand insertLocCmd = new SqlCommand(insertLocQuery, connection))
						{
							insertLocCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
							insertLocCmd.Parameters.Add(new SqlParameter("@MachineName", machineName));
							insertLocCmd.Parameters.Add(new SqlParameter("@MachineIp", (object)machineIp ?? DBNull.Value));
							insertLocCmd.Parameters.Add(new SqlParameter("@SourcePath", (object)sourcePath ?? DBNull.Value));
							insertLocCmd.Parameters.Add(new SqlParameter("@LastSeenAt", lastSeen));

							insertLocCmd.ExecuteNonQuery();
						}
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при сохранении сертификата " + record.SerialNumber + " в БД: " + ex.Message, ex);
				return false;
			}
		}

		// Загрузка данных из БД с использованием LEFT JOIN между Certificates и CertificateLocations
		public static List<CertificateRecord> LoadCertificatesFromDatabase(string connectionString, string tableName = "Certificates")
		{
			List<CertificateRecord> result = new List<CertificateRecord>();
			Dictionary<string, CertificateRecord> certDict = new Dictionary<string, CertificateRecord>(StringComparer.OrdinalIgnoreCase);

			try
			{
				if (string.IsNullOrWhiteSpace(connectionString))
				{
					return result;
				}

				if (string.IsNullOrWhiteSpace(tableName))
				{
					tableName = "Certificates";
				}

				EnsureDatabaseExists(connectionString);

				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();
					EnsureTablesExist(connection, tableName);

					string query = @"
						SELECT 
							c.[SerialNumber],
							c.[CreatedAt],
							c.[UpdatedAt],
							c.[OwnerName],
							c.[IssuerName],
							c.[CryptoProVersion],
							c.[HasEmbeddedLicense],
							loc.[MachineName],
							loc.[MachineIp],
							loc.[SourcePath],
							loc.[LastSeenAt]
						FROM [dbo].[" + tableName + @"] c
						LEFT JOIN [dbo].[CertificateLocations] loc ON c.[SerialNumber] = loc.[SerialNumber]
						ORDER BY c.[OwnerName], c.[SerialNumber], loc.[MachineName]";

					using (SqlCommand command = new SqlCommand(query, connection))
					using (SqlDataReader reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							string serialNumber = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
							if (string.IsNullOrWhiteSpace(serialNumber))
							{
								continue;
							}

							CertificateRecord certRecord;
							if (!certDict.TryGetValue(serialNumber, out certRecord))
							{
								certRecord = new CertificateRecord()
								{
									SerialNumber = serialNumber,
									CreatedAt = reader.IsDBNull(1) ? DateTime.Now : reader.GetDateTime(1),
									UpdatedAt = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2),
									OwnerName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
									IssuerName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
									CryptoProVersion = reader.IsDBNull(5) ? "—" : reader.GetString(5),
									HasEmbeddedLicense = !reader.IsDBNull(6) && reader.GetBoolean(6),
									Locations = new List<CertificateLocationRecord>()
								};

								certDict[serialNumber] = certRecord;
								result.Add(certRecord);
							}

							if (!reader.IsDBNull(7))
							{
								string locMachineName = reader.GetString(7);
								string locMachineIp = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
								string locSourcePath = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
								DateTime locLastSeen = reader.IsDBNull(10) ? DateTime.Now : reader.GetDateTime(10);

								certRecord.AddLocation(locMachineName, locMachineIp, locSourcePath, locLastSeen);

								if (string.IsNullOrEmpty(certRecord.MachineName))
								{
									certRecord.MachineName = locMachineName;
									certRecord.MachineIp = locMachineIp;
									certRecord.SourcePath = locSourcePath;
								}
							}
						}
					}
				}

				Logger.LogInfo("Успешно восстановлено сертификатов из базы данных MSSQL через LEFT JOIN: " + result.Count);
			}
			catch (Exception ex)
			{
				Logger.LogError("Сбой при чтении сертификатов из БД через LEFT JOIN: " + ex.Message, ex);
			}

			return result;
		}
	}
}
