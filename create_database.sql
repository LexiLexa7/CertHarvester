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

-- Основная таблица: один сертификат = одна строка (уникальность по серийному номеру, дублей быть не может)
IF OBJECT_ID(N'[dbo].[Certificates]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[Certificates] (
		[SerialNumber] NVARCHAR(100) NOT NULL PRIMARY KEY,   -- серийный номер сертификата
		[DateInserted] DATETIME NOT NULL,                    -- дата и время первого внесения
		[DateUpdated] DATETIME NOT NULL,                     -- дата и время последнего обновления (при первом внесении = DateInserted)
		[OwnerName] NVARCHAR(500) NULL,                      -- имя владельца
		[Issuer] NVARCHAR(500) NULL,                         -- удостоверяющий центр
		[CryptoProVersion] NVARCHAR(100) NULL,               -- версия КриптоПро, если это лицензия КриптоПро
		[HasEmbeddedLicense] BIT NOT NULL                    -- встроена ли лицензия КриптоПро в сертификат
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
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_Issuer' AND object_id = OBJECT_ID(N'[dbo].[Certificates]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_Issuer] ON [dbo].[Certificates] ([Issuer]);
GO

-- Поиск устаревших записей по дате обновления
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_DateUpdated' AND object_id = OBJECT_ID(N'[dbo].[Certificates]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_DateUpdated] ON [dbo].[Certificates] ([DateUpdated]);
GO

-- История по серийному номеру
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Certificates_History_SerialNumber' AND object_id = OBJECT_ID(N'[dbo].[Certificates_History]'))
	CREATE NONCLUSTERED INDEX [IX_Certificates_History_SerialNumber] ON [dbo].[Certificates_History] ([SerialNumber]);
GO

-- Дата обновления не может быть раньше даты внесения
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Certificates_Dates')
	ALTER TABLE [dbo].[Certificates] ADD CONSTRAINT [CK_Certificates_Dates] CHECK ([DateUpdated] >= [DateInserted]);
GO


-- =====================================================================
-- 4. ПРЕДСТАВЛЕНИЯ (VIEWS) ДЛЯ ОТЧЁТОВ
-- =====================================================================

-- Все сертификаты в удобном виде: текст вместо 0/1 и сколько дней данные не обновлялись
CREATE OR ALTER VIEW [dbo].[vw_CertificatesFull]
AS
SELECT
	[SerialNumber],
	[OwnerName],
	[Issuer],
	[CryptoProVersion],
	[HasEmbeddedLicense],
	CASE WHEN [HasEmbeddedLicense] = 1 THEN N'Да' ELSE N'Нет' END AS [EmbeddedLicenseText],
	[DateInserted],
	[DateUpdated],
	DATEDIFF(DAY, [DateUpdated], GETDATE()) AS [DaysSinceUpdate]
FROM [dbo].[Certificates];
GO

-- Только лицензии КриптоПро (там, где указана версия)
CREATE OR ALTER VIEW [dbo].[vw_CryptoProLicenses]
AS
SELECT
	[SerialNumber],
	[OwnerName],
	[Issuer],
	[CryptoProVersion],
	[HasEmbeddedLicense],
	[DateUpdated]
FROM [dbo].[Certificates]
WHERE [CryptoProVersion] IS NOT NULL;
GO

-- Статистика по удостоверяющим центрам
CREATE OR ALTER VIEW [dbo].[vw_IssuerStatistics]
AS
SELECT
	ISNULL([Issuer], N'(не указан)') AS [Issuer],
	COUNT(*) AS [CertificatesCount],
	SUM(CASE WHEN [HasEmbeddedLicense] = 1 THEN 1 ELSE 0 END) AS [WithEmbeddedLicenseCount],
	MAX([DateUpdated]) AS [LastUpdate]
FROM [dbo].[Certificates]
GROUP BY ISNULL([Issuer], N'(не указан)');
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
-- Возвращает одну строку: SerialNumber и Action ('INSERTED' или 'UPDATED').
-- Пример: EXEC dbo.usp_UpsertCertificate N'0123', N'Иванов И.И.', N'УЦ Пример', N'5.0.12000', 1;
CREATE OR ALTER PROCEDURE [dbo].[usp_UpsertCertificate]
	@SerialNumber NVARCHAR(100),
	@OwnerName NVARCHAR(500) = NULL,
	@Issuer NVARCHAR(500) = NULL,
	@CryptoProVersion NVARCHAR(100) = NULL,
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
		existing.[OwnerName], existing.[Issuer], existing.[CryptoProVersion], existing.[HasEmbeddedLicense],
		@OwnerName, @Issuer, @CryptoProVersion, @HasEmbeddedLicense
	FROM [dbo].[Certificates] AS existing WITH (UPDLOCK, HOLDLOCK)
	WHERE existing.[SerialNumber] = @SerialNumber
		AND EXISTS (
			SELECT existing.[OwnerName], existing.[Issuer], existing.[CryptoProVersion], existing.[HasEmbeddedLicense]
			EXCEPT
			SELECT @OwnerName, @Issuer, @CryptoProVersion, @HasEmbeddedLicense
		);

	-- Пробуем обновить существующую строку
	UPDATE [dbo].[Certificates]
	SET [DateUpdated] = @CurrentTimestamp,
		[OwnerName] = @OwnerName,
		[Issuer] = @Issuer,
		[CryptoProVersion] = @CryptoProVersion,
		[HasEmbeddedLicense] = @HasEmbeddedLicense
	WHERE [SerialNumber] = @SerialNumber;

	-- Если обновлять было нечего, вставляем новую строку (DateUpdated = DateInserted)
	IF @@ROWCOUNT = 0
	BEGIN
		INSERT INTO [dbo].[Certificates]
			([SerialNumber], [DateInserted], [DateUpdated], [OwnerName], [Issuer], [CryptoProVersion], [HasEmbeddedLicense])
		VALUES
			(@SerialNumber, @CurrentTimestamp, @CurrentTimestamp, @OwnerName, @Issuer, @CryptoProVersion, @HasEmbeddedLicense);

		SET @ActionName = N'INSERTED';
	END;

	COMMIT TRANSACTION;

	SELECT @SerialNumber AS [SerialNumber], @ActionName AS [Action];
END;
GO

-- Список сертификатов, данные по которым не обновлялись заданное число дней (по умолчанию 30).
-- Пример: EXEC dbo.usp_GetStaleCertificates @DaysWithoutUpdate = 60;
CREATE OR ALTER PROCEDURE [dbo].[usp_GetStaleCertificates]
	@DaysWithoutUpdate INT = 30
AS
BEGIN
	SET NOCOUNT ON;

	SELECT [SerialNumber], [OwnerName], [Issuer], [DateUpdated],
		DATEDIFF(DAY, [DateUpdated], GETDATE()) AS [DaysSinceUpdate]
	FROM [dbo].[Certificates]
	WHERE [DateUpdated] < DATEADD(DAY, -@DaysWithoutUpdate, GETDATE())
	ORDER BY [DateUpdated];
END;
GO

-- Поиск сертификатов по части имени владельца или названия УЦ.
-- Пример: EXEC dbo.usp_SearchCertificates @SearchText = N'Иванов';
CREATE OR ALTER PROCEDURE [dbo].[usp_SearchCertificates]
	@SearchText NVARCHAR(200)
AS
BEGIN
	SET NOCOUNT ON;

	SELECT [SerialNumber], [OwnerName], [Issuer], [CryptoProVersion], [HasEmbeddedLicense], [DateInserted], [DateUpdated]
	FROM [dbo].[Certificates]
	WHERE [OwnerName] LIKE N'%' + @SearchText + N'%'
		OR [Issuer] LIKE N'%' + @SearchText + N'%'
	ORDER BY [OwnerName];
END;
GO


-- =====================================================================
-- 6. РОЛЬ ДЛЯ УЧЁТНОЙ ЗАПИСИ ПРОГРАММЫ
-- =====================================================================
-- Роль с минимальными правами: читать, вставлять, обновлять, создавать таблицы в схеме dbo.
-- Пароли в git не хранятся. Чтобы подключить пользователя, выполни отдельно (подставь свои данные):
--     CREATE LOGIN [certharvester_app] WITH PASSWORD = N'<придумай пароль>';
--     CREATE USER [certharvester_app] FOR LOGIN [certharvester_app];
--     ALTER ROLE [CertHarvesterAppRole] ADD MEMBER [certharvester_app];
IF DATABASE_PRINCIPAL_ID(N'CertHarvesterAppRole') IS NULL
BEGIN
	CREATE ROLE [CertHarvesterAppRole];
END;
GO

GRANT SELECT, INSERT, UPDATE, EXECUTE ON SCHEMA::[dbo] TO [CertHarvesterAppRole];
GRANT CREATE TABLE TO [CertHarvesterAppRole];
GRANT ALTER ON SCHEMA::[dbo] TO [CertHarvesterAppRole];
GO


-- =====================================================================
-- 7. ТЕСТОВЫЕ ДАННЫЕ (вымышленные), добавляются только если такого серийного номера ещё нет
-- =====================================================================
INSERT INTO [dbo].[Certificates]
	([SerialNumber], [DateInserted], [DateUpdated], [OwnerName], [Issuer], [CryptoProVersion], [HasEmbeddedLicense])
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
) AS testData ([SerialNumber], [DateInserted], [DateUpdated], [OwnerName], [Issuer], [CryptoProVersion], [HasEmbeddedLicense])
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[Certificates] AS existing WHERE existing.[SerialNumber] = testData.[SerialNumber]
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
SELECT * FROM [dbo].[vw_CertificatesFull] ORDER BY [DateUpdated] DESC;
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
