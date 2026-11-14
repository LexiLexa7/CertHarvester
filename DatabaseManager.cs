using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace CertHarvester
{
	/// <summary>
	/// Класс управления взаимодействием с базой данных Microsoft SQL Server 2019
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

		// Проверка существования целевой таблицы в БД и ее автоматическое создание при отсутствии
		public static void EnsureTableExists(SqlConnection connection, string tableName)
		{
			string checkTableQuery = @"
				IF NOT EXISTS (
					SELECT * FROM INFORMATION_SCHEMA.TABLES 
					WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TargetTableName
				)
				BEGIN
					CREATE TABLE [dbo].[" + tableName + @"] (
						[SerialNumber] NVARCHAR(100) NOT NULL PRIMARY KEY,
						[DateInserted] DATETIME NOT NULL,
						[DateUpdated] DATETIME NOT NULL,
						[OwnerName] NVARCHAR(500) NULL,
						[Issuer] NVARCHAR(500) NULL,
						[CryptoProVersion] NVARCHAR(100) NULL,
						[HasEmbeddedLicense] BIT NOT NULL
					);
				END";

			using (SqlCommand command = new SqlCommand(checkTableQuery, connection))
			{
				command.Parameters.Add(new SqlParameter("@TargetTableName", tableName));
				command.ExecuteNonQuery();
				Logger.LogInfo("Проверка структуры базы данных выполнена. Таблица '" + tableName + "' готова к записи данных");
			}
		}

		// Сохранение списка сертификатов в БД с проверкой дубликатов (UPDATE при наличии, INSERT при отсутствии)
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

					// 2. Обязательная проверка существования таблицы и создание при необходимости
					EnsureTableExists(connection, tableName);

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

		// Вставка новой записи либо обновление существующей строки по уникальному серийному номеру без дублирования
		private static bool UpsertCertificateRecord(SqlConnection connection, CertificateRecord record, string tableName)
		{
			try
			{
				// Проверка наличия записи с данным серийным номером в таблице
				string selectQuery = "SELECT COUNT(*) FROM [dbo].[" + tableName + "] WHERE [SerialNumber] = @SerialNumber";
				bool recordExists = false;

				using (SqlCommand checkCmd = new SqlCommand(selectQuery, connection))
				{
					checkCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
					int count = Convert.ToInt32(checkCmd.ExecuteScalar());
					recordExists = count > 0;
				}

				DateTime currentTimestamp = DateTime.Now;

				if (recordExists == true)
				{
					// Если сертификат уже существует, обновляем данные и актуализируем дату обновления
					string updateQuery = @"
						UPDATE [dbo].[" + tableName + @"] 
						SET [DateUpdated] = @DateUpdated,
						    [OwnerName] = @OwnerName,
						    [Issuer] = @Issuer,
						    [CryptoProVersion] = @CryptoProVersion,
						    [HasEmbeddedLicense] = @HasEmbeddedLicense
						WHERE [SerialNumber] = @SerialNumber";

					using (SqlCommand updateCmd = new SqlCommand(updateQuery, connection))
					{
						updateCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
						updateCmd.Parameters.Add(new SqlParameter("@DateUpdated", currentTimestamp));
						updateCmd.Parameters.Add(new SqlParameter("@OwnerName", (object)record.OwnerName ?? DBNull.Value));
						updateCmd.Parameters.Add(new SqlParameter("@Issuer", (object)record.Issuer ?? DBNull.Value));
						updateCmd.Parameters.Add(new SqlParameter("@CryptoProVersion", (object)record.CryptoProVersion ?? DBNull.Value));
						updateCmd.Parameters.Add(new SqlParameter("@HasEmbeddedLicense", record.HasEmbeddedLicense));

						updateCmd.ExecuteNonQuery();
					}

					Logger.LogInfo("Обновлен существующий сертификат в БД: " + record.SerialNumber);
				}
				else
				{
					// Если сертификат новый, вставляем строку: DateUpdated строго равно DateInserted
					string insertQuery = @"
						INSERT INTO [dbo].[" + tableName + @"] 
						([SerialNumber], [DateInserted], [DateUpdated], [OwnerName], [Issuer], [CryptoProVersion], [HasEmbeddedLicense])
						VALUES 
						(@SerialNumber, @DateInserted, @DateUpdated, @OwnerName, @Issuer, @CryptoProVersion, @HasEmbeddedLicense)";

					using (SqlCommand insertCmd = new SqlCommand(insertQuery, connection))
					{
						insertCmd.Parameters.Add(new SqlParameter("@SerialNumber", record.SerialNumber));
						insertCmd.Parameters.Add(new SqlParameter("@DateInserted", currentTimestamp));
						insertCmd.Parameters.Add(new SqlParameter("@DateUpdated", currentTimestamp));
						insertCmd.Parameters.Add(new SqlParameter("@OwnerName", (object)record.OwnerName ?? DBNull.Value));
						insertCmd.Parameters.Add(new SqlParameter("@Issuer", (object)record.Issuer ?? DBNull.Value));
						insertCmd.Parameters.Add(new SqlParameter("@CryptoProVersion", (object)record.CryptoProVersion ?? DBNull.Value));
						insertCmd.Parameters.Add(new SqlParameter("@HasEmbeddedLicense", record.HasEmbeddedLicense));

						insertCmd.ExecuteNonQuery();
					}

					Logger.LogInfo("Внесен новый сертификат в БД: " + record.SerialNumber);
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при сохранении сертификата " + record.SerialNumber + " в БД: " + ex.Message, ex);
				return false;
			}
		}
	}
}
