/*
	CertHarvester: полный скрипт базы данных (Microsoft SQL Server 2016 и новее, проверено по синтаксису под 2019)

	Что делает скрипт:
		1. Создаёт базу CertHarvesterDB
		2. Создаёт таблицы Certificates и Certificates_History (структура 1 в 1 как создаёт программа в DatabaseManager.cs)
		3. Создаёт индексы и проверочное ограничение дат
		4. Создаёт представления (views) для отчётов
		5. Создаёт хранимые процедуры (вставка/обновление сертификата, поиск устаревших записей)
		6. Создаёт роль для учётной записи программы с минимально нужными правами
		7. Добавляет тестовые данные (вымышленные)

	Как запускать: SQL Server Management Studio -> Файл -> Открыть -> Файл -> F5.
	Скрипт можно запускать повторно: ничего не задвоится и не сломается.
	Программа на C# работает с этой базой без изменений: если таблицы уже есть, она просто пишет в них.
*/


-- =====================================================================
-- 1. БАЗА ДАННЫХ
-- =====================================================================
IF DB_ID(N'CertHarvesterDB') IS NULL
BEGIN
	CREATE DATABASE [CertHarvesterDB];
END;
GO

USE [CertHarvesterDB];
GO


-- =====================================================================
-- 2. ТАБЛИЦЫ
-- =====================================================================

-- Основная таблица строго по ТЗ (7 столбцов): один сертификат = одна строка (уникальность по SerialNumber)
IF OBJECT_ID(N'[dbo].[Certificates]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[Certificates] (
		[SerialNumber] NVARCHAR(128) NOT NULL PRIMARY KEY,   -- серийный номер сертификата
		[CreatedAt] DATETIME NOT NULL,                      -- дата и время внесения записи (по ТЗ)
		[UpdatedAt] DATETIME NOT NULL,                      -- дата и время последнего обновления (по ТЗ)
		[OwnerName] NVARCHAR(512) NULL,                      -- имя владельца (по ТЗ)
		[IssuerName] NVARCHAR(512) NULL,                     -- удостоверяющий центр (по ТЗ)
		[CryptoProVersion] NVARCHAR(64) NULL,                -- версия КриптоПро CSP (по ТЗ)
		[HasEmbeddedLicense] BIT NOT NULL                    -- наличие встроенной лицензии (по ТЗ)
	);
END;
GO

-- Вспомогательная таблица привязки сертификатов к компьютерам сети (нормализованная архитектура)
IF OBJECT_ID(N'[dbo].[CertificateLocations]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[CertificateLocations] (
		[SerialNumber] NVARCHAR(128) NOT NULL,
		[MachineName] NVARCHAR(128) NOT NULL,
		[MachineIp] NVARCHAR(64) NULL,
		[SourcePath] NVARCHAR(512) NULL,
		[LastSeenAt] DATETIME NOT NULL,
		CONSTRAINT [PK_CertificateLocations] PRIMARY KEY ([SerialNumber], [MachineName]),
		CONSTRAINT [FK_CertificateLocations_Certificates] FOREIGN KEY ([SerialNumber]) REFERENCES [dbo].[Certificates] ([SerialNumber]) ON DELETE CASCADE
	);
END;
GO

-- Таблица аудита: сюда попадают старые и новые значения, если данные сертификата изменились
IF OBJECT_ID(N'[dbo].[Certificates_History]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[Certificates_History] (
		[HistoryId] INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
		[SerialNumber] NVARCHAR(100) NOT NULL,
		[ChangeDate] DATETIME NOT NULL,
		[OldOwnerName] NVARCHAR(500) NULL,
		[OldIssuer] NVARCHAR(500) NULL,
		[OldCryptoProVersion] NVARCHAR(100) NULL,
		[OldHasEmbeddedLicense] BIT NULL,
		[NewOwnerName] NVARCHAR(500) NULL,
		[NewIssuer] NVARCHAR(500) NULL,
		[NewCryptoProVersion] NVARCHAR(100) NULL,
		[NewHasEmbeddedLicense] BIT NULL
	);
END;
GO


-- =====================================================================
-- 3. ИНДЕКСЫ И ОГРАНИЧЕНИЯ
-- =====================================================================

-- Поиск по владельцу
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_OwnerName' AND object_id = OBJECT_ID(N'[dbo].[Certificates]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_OwnerName] ON [dbo].[Certificates] ([OwnerName]);
GO

-- Поиск по удостоверяющему центру
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_IssuerName' AND object_id = OBJECT_ID(N'[dbo].[Certificates]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_IssuerName] ON [dbo].[Certificates] ([IssuerName]);
GO

-- Поиск устаревших записей по дате обновления
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_UpdatedAt' AND object_id = OBJECT_ID(N'[dbo].[Certificates]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_UpdatedAt] ON [dbo].[Certificates] ([UpdatedAt]);
GO

-- Индекс по локациям: поиск по компьютеру сети
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CertificateLocations_MachineName' AND object_id = OBJECT_ID(N'[dbo].[CertificateLocations]'))
	CREATE NONCLUSTERED INDEX [IX_CertificateLocations_MachineName] ON [dbo].[CertificateLocations] ([MachineName]);
GO

-- История по серийному номеру
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_History_SerialNumber' AND object_id = OBJECT_ID(N'[dbo].[Certificates_History]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_History_SerialNumber] ON [dbo].[Certificates_History] ([SerialNumber]);
GO

-- Дата обновления не может быть раньше даты внесения
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Certificates_Dates')
	ALTER TABLE [dbo].[Certificates] ADD CONSTRAINT [CK_Certificates_Dates] CHECK ([UpdatedAt] >= [CreatedAt]);
GO


-- =====================================================================
-- 4. ПРЕДСТАВЛЕНИЯ (VIEWS) ДЛЯ ОТЧЁТОВ
-- =====================================================================

-- Все сертификаты в удобном виде с привязкой к компьютерам сети (LEFT JOIN)
CREATE OR ALTER VIEW [dbo].[vw_CertificatesFull]
AS
SELECT
	c.[SerialNumber],
	c.[OwnerName],
	c.[IssuerName],
	c.[CryptoProVersion],
	c.[HasEmbeddedLicense],
	CASE WHEN c.[HasEmbeddedLicense] = 1 THEN N'Да' ELSE N'Нет' END AS [EmbeddedLicenseText],
	c.[CreatedAt],
	c.[UpdatedAt],
	DATEDIFF(DAY, c.[UpdatedAt], GETDATE()) AS [DaysSinceUpdate],
	loc.[MachineName],
	loc.[MachineIp],
	loc.[SourcePath],
	loc.[LastSeenAt]
FROM [dbo].[Certificates] c
LEFT JOIN [dbo].[CertificateLocations] loc ON c.[SerialNumber] = loc.[SerialNumber];
GO

-- Только лицензии КриптоПро (там, где указана версия)
CREATE OR ALTER VIEW [dbo].[vw_CryptoProLicenses]
AS
SELECT
	[SerialNumber],
	[OwnerName],
	[IssuerName],
	[CryptoProVersion],
	[HasEmbeddedLicense],
	[UpdatedAt]
FROM [dbo].[Certificates]
WHERE [CryptoProVersion] IS NOT NULL;
GO

-- Статистика по удостоверяющим центрам
CREATE OR ALTER VIEW [dbo].[vw_IssuerStatistics]
AS
SELECT
	ISNULL([IssuerName], N'(не указан)') AS [IssuerName],
	COUNT(*) AS [CertificatesCount],
	SUM(CASE WHEN [HasEmbeddedLicense] = 1 THEN 1 ELSE 0 END) AS [WithEmbeddedLicenseCount],
	MAX([UpdatedAt]) AS [LastUpdate]
FROM [dbo].[Certificates]
GROUP BY ISNULL([IssuerName], N'(не указан)');
GO

-- Журнал изменений сертификатов: что было и что стало
CREATE OR ALTER VIEW [dbo].[vw_CertificateChanges]
AS
SELECT
	history.[HistoryId],
	history.[SerialNumber],
	history.[ChangeDate],
	history.[OldOwnerName],
	history.[NewOwnerName],
	history.[OldIssuer],
	history.[NewIssuer],
	history.[OldCryptoProVersion],
	history.[NewCryptoProVersion],
	history.[OldHasEmbeddedLicense],
	history.[NewHasEmbeddedLicense]
FROM [dbo].[Certificates_History] AS history;
GO


-- =====================================================================
-- 5. ХРАНИМЫЕ ПРОЦЕДУРЫ
-- =====================================================================

-- Вставка нового сертификата или обновление существующего (по серийному номеру, без дублей).
-- Если данные изменились, старые значения записываются в Certificates_History.
CREATE OR ALTER PROCEDURE [dbo].[usp_UpsertCertificate]
	@SerialNumber NVARCHAR(128),
	@OwnerName NVARCHAR(512) = NULL,
	@IssuerName NVARCHAR(512) = NULL,
	@CryptoProVersion NVARCHAR(64) = NULL,
	@HasEmbeddedLicense BIT = 0
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @CurrentTimestamp DATETIME = GETDATE();
	DECLARE @ActionName NVARCHAR(10) = N'UPDATED';

	BEGIN TRANSACTION;

	-- Сохраняем старые значения в аудит, только если данные реально изменились
	INSERT INTO [dbo].[Certificates_History]
		([SerialNumber], [ChangeDate],
		[OldOwnerName], [OldIssuer], [OldCryptoProVersion], [OldHasEmbeddedLicense],
		[NewOwnerName], [NewIssuer], [NewCryptoProVersion], [NewHasEmbeddedLicense])
	SELECT existing.[SerialNumber], @CurrentTimestamp,
		existing.[OwnerName], existing.[IssuerName], existing.[CryptoProVersion], existing.[HasEmbeddedLicense],
		@OwnerName, @IssuerName, @CryptoProVersion, @HasEmbeddedLicense
	FROM [dbo].[Certificates] AS existing WITH (UPDLOCK, HOLDLOCK)
	WHERE existing.[SerialNumber] = @SerialNumber
		AND EXISTS (
			SELECT existing.[OwnerName], existing.[IssuerName], existing.[CryptoProVersion], existing.[HasEmbeddedLicense]
			EXCEPT
			SELECT @OwnerName, @IssuerName, @CryptoProVersion, @HasEmbeddedLicense
		);

	-- Пробуем обновить существующую строку
	UPDATE [dbo].[Certificates]
	SET [UpdatedAt] = @CurrentTimestamp,
		[OwnerName] = @OwnerName,
		[IssuerName] = @IssuerName,
		[CryptoProVersion] = @CryptoProVersion,
		[HasEmbeddedLicense] = @HasEmbeddedLicense
	WHERE [SerialNumber] = @SerialNumber;

	-- Если обновлять было нечего, вставляем новую строку
	IF @@ROWCOUNT = 0
	BEGIN
		INSERT INTO [dbo].[Certificates]
			([SerialNumber], [CreatedAt], [UpdatedAt], [OwnerName], [IssuerName], [CryptoProVersion], [HasEmbeddedLicense])
		VALUES
			(@SerialNumber, @CurrentTimestamp, @CurrentTimestamp, @OwnerName, @IssuerName, @CryptoProVersion, @HasEmbeddedLicense);

		SET @ActionName = N'INSERTED';
	END;

	COMMIT TRANSACTION;

	SELECT @SerialNumber AS [SerialNumber], @ActionName AS [Action];
END;
GO

-- Список сертификатов, данные по которым не обновлялись заданное число дней (по умолчанию 30).
CREATE OR ALTER PROCEDURE [dbo].[usp_GetStaleCertificates]
	@DaysWithoutUpdate INT = 30
AS
BEGIN
	SET NOCOUNT ON;

	SELECT [SerialNumber], [OwnerName], [IssuerName], [UpdatedAt],
		DATEDIFF(DAY, [UpdatedAt], GETDATE()) AS [DaysSinceUpdate]
	FROM [dbo].[Certificates]
	WHERE [UpdatedAt] < DATEADD(DAY, -@DaysWithoutUpdate, GETDATE())
	ORDER BY [UpdatedAt];
END;
GO

-- Поиск сертификатов по части имени владельца или названия УЦ.
CREATE OR ALTER PROCEDURE [dbo].[usp_SearchCertificates]
	@SearchText NVARCHAR(200)
AS
BEGIN
	SET NOCOUNT ON;

	SELECT [SerialNumber], [OwnerName], [IssuerName], [CryptoProVersion], [HasEmbeddedLicense], [CreatedAt], [UpdatedAt]
	FROM [dbo].[Certificates]
	WHERE [OwnerName] LIKE N'%' + @SearchText + N'%'
		OR [IssuerName] LIKE N'%' + @SearchText + N'%'
	ORDER BY [OwnerName];
END;
GO


-- =====================================================================
-- 6. РОЛЬ ДЛЯ УЧЁТНОЙ ЗАПИСИ ПРОГРАММЫ
-- =====================================================================
IF DATABASE_PRINCIPAL_ID(N'CertHarvesterAppRole') IS NULL
BEGIN
	CREATE ROLE [CertHarvesterAppRole];
END;
GO

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[dbo] TO [CertHarvesterAppRole];
GRANT CREATE TABLE TO [CertHarvesterAppRole];
GRANT ALTER ON SCHEMA::[dbo] TO [CertHarvesterAppRole];
GO


-- =====================================================================
-- 7. ТЕСТОВЫЕ ДАННЫЕ (вымышленные), добавляются только если такого серийного номера ещё нет
-- =====================================================================
INSERT INTO [dbo].[Certificates]
	([SerialNumber], [CreatedAt], [UpdatedAt], [OwnerName], [IssuerName], [CryptoProVersion], [HasEmbeddedLicense])
SELECT testData.*
FROM (VALUES
	(N'01A2B3C4D5E6F70811223344', DATEADD(DAY, -120, GETDATE()), DATEADD(DAY, -2, GETDATE()),   N'Иванов Иван Иванович',          N'АО "Тестовый удостоверяющий центр"', N'5.0.12000', CAST(1 AS BIT)),
	(N'02B3C4D5E6F7081922334455', DATEADD(DAY, -110, GETDATE()), DATEADD(DAY, -110, GETDATE()), N'Петрова Анна Сергеевна',        N'ООО "УЦ Пример"',                    N'4.0.9963',  CAST(0 AS BIT)),
	(N'03C4D5E6F708192A33445566', DATEADD(DAY, -95, GETDATE()),  DATEADD(DAY, -1, GETDATE()),   N'Сидоров Алексей Петрович',      N'АО "Тестовый удостоверяющий центр"', NULL,         CAST(0 AS BIT)),
	(N'04D5E6F708192A3B44556677', DATEADD(DAY, -90, GETDATE()),  DATEADD(DAY, -45, GETDATE()),  N'Кузнецова Мария Дмитриевна',    N'ФГУП "ГРЧЦ" (тестовый УЦ)',          N'5.0.11998', CAST(1 AS BIT)),
	(N'05E6F708192A3B4C55667788', DATEADD(DAY, -80, GETDATE()),  DATEADD(DAY, -3, GETDATE()),   N'Смирнов Дмитрий Олегович',      N'ООО "УЦ Пример"',                    NULL,         CAST(0 AS BIT)),
	(N'06F708192A3B4C5D66778899', DATEADD(DAY, -75, GETDATE()),  DATEADD(DAY, -75, GETDATE()),  N'Попова Елена Викторовна',       N'ФГУП "ГРЧЦ" (тестовый УЦ)',          N'4.0.9963',  CAST(0 AS BIT)),
	(N'0708192A3B4C5D6E778899AA', DATEADD(DAY, -60, GETDATE()),  DATEADD(DAY, -5, GETDATE()),   N'Васильев Павел Андреевич',      N'АО "Тестовый удостоверяющий центр"', N'5.0.12000', CAST(1 AS BIT)),
	(N'08192A3B4C5D6E7F8899AABB', DATEADD(DAY, -50, GETDATE()),  DATEADD(DAY, -50, GETDATE()),  N'Новикова Ольга Игоревна',       N'ООО "УЦ Пример"',                    NULL,         CAST(0 AS BIT)),
	(N'092A3B4C5D6E7F8099AABBCC', DATEADD(DAY, -40, GETDATE()),  DATEADD(DAY, -4, GETDATE()),   N'Морозов Сергей Николаевич',     N'ФГУП "ГРЧЦ" (тестовый УЦ)',          N'5.0.12000', CAST(0 AS BIT)),
	(N'0A3B4C5D6E7F80919AABBCCD', DATEADD(DAY, -30, GETDATE()),  DATEADD(DAY, -30, GETDATE()),  N'Волкова Татьяна Андреевна',     N'АО "Тестовый удостоверяющий центр"', NULL,         CAST(0 AS BIT)),
	(N'0B4C5D6E7F8091A2ABBCCDDE', DATEADD(DAY, -20, GETDATE()),  DATEADD(DAY, -1, GETDATE()),   N'Алексеев Роман Евгеньевич',     N'ООО "УЦ Пример"',                    N'5.0.11998', CAST(1 AS BIT)),
	(N'0C5D6E7F8091A2B3BBCCDDEF', DATEADD(DAY, -10, GETDATE()),  DATEADD(DAY, -10, GETDATE()),  N'Лебедева Ксения Романовна',     N'ФГУП "ГРЧЦ" (тестовый УЦ)',          NULL,         CAST(0 AS BIT))
) AS testData ([SerialNumber], [CreatedAt], [UpdatedAt], [OwnerName], [IssuerName], [CryptoProVersion], [HasEmbeddedLicense])
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[Certificates] AS existing WHERE existing.[SerialNumber] = testData.[SerialNumber]
);
GO

-- Добавление тестовых привязок сертификатов к компьютерам сети
INSERT INTO [dbo].[CertificateLocations]
	([SerialNumber], [MachineName], [MachineIp], [SourcePath], [LastSeenAt])
SELECT locData.*
FROM (VALUES
	(N'01A2B3C4D5E6F70811223344', N'DESKTOP-R10QO9B', N'127.0.0.1', N'[Хранилище: CurrentUser\My]', GETDATE()),
	(N'01A2B3C4D5E6F70811223344', N'PC-NODE-1', N'192.168.1.101', N'[Реестр: HKLM\Crypto Pro\Settings\Keys]', GETDATE()),
	(N'02B3C4D5E6F7081922334455', N'DESKTOP-R10QO9B', N'127.0.0.1', N'[Хранилище: CurrentUser\My]', GETDATE()),
	(N'04D5E6F708192A3B44556677', N'PC-NODE-2', N'192.168.1.102', N'[Хранилище: LocalMachine\My]', GETDATE())
) AS locData ([SerialNumber], [MachineName], [MachineIp], [SourcePath], [LastSeenAt])
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[CertificateLocations] AS existing 
	WHERE existing.[SerialNumber] = locData.[SerialNumber] AND existing.[MachineName] = locData.[MachineName]
);
GO

-- Пара записей в историю изменений для примера (чтобы представление vw_CertificateChanges не было пустым)
INSERT INTO [dbo].[Certificates_History]
	([SerialNumber], [ChangeDate],
	[OldOwnerName], [OldIssuer], [OldCryptoProVersion], [OldHasEmbeddedLicense],
	[NewOwnerName], [NewIssuer], [NewCryptoProVersion], [NewHasEmbeddedLicense])
SELECT historyData.*
FROM (VALUES
	(N'01A2B3C4D5E6F70811223344', DATEADD(DAY, -2, GETDATE()),  N'Иванов Иван Иванович', N'АО "Тестовый удостоверяющий центр"', N'4.0.9963', CAST(0 AS BIT),
		N'Иванов Иван Иванович', N'АО "Тестовый удостоверяющий центр"', N'5.0.12000', CAST(1 AS BIT)),
	(N'0B4C5D6E7F8091A2ABBCCDDE', DATEADD(DAY, -1, GETDATE()),  N'Алексеев Роман Евгеньевич', N'ООО "УЦ Пример"', NULL, CAST(0 AS BIT),
		N'Алексеев Роман Евгеньевич', N'ООО "УЦ Пример"', N'5.0.11998', CAST(1 AS BIT))
) AS historyData ([SerialNumber], [ChangeDate], [OldOwnerName], [OldIssuer], [OldCryptoProVersion], [OldHasEmbeddedLicense],
	[NewOwnerName], [NewIssuer], [NewCryptoProVersion], [NewHasEmbeddedLicense])
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[Certificates_History] AS existing WHERE existing.[SerialNumber] = historyData.[SerialNumber]
);
GO


-- =====================================================================
-- 8. ПРОВЕРКА: что получилось (можно выделить любой запрос и нажать F5)
-- =====================================================================
SELECT * FROM [dbo].[vw_CertificatesFull] ORDER BY [UpdatedAt] DESC;
SELECT * FROM [dbo].[vw_IssuerStatistics];
SELECT * FROM [dbo].[vw_CryptoProLicenses];
SELECT * FROM [dbo].[vw_CertificateChanges];
EXEC [dbo].[usp_GetStaleCertificates] @DaysWithoutUpdate = 30;
GO

/*
	Очистка тестовых данных (раскомментируй и выполни, если нужно начать с пустой таблицы):

	DELETE FROM [dbo].[Certificates_History];
	DELETE FROM [dbo].[Certificates];
*/
