CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `application_logs` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Level` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Category` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Message` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
        `Exception` varchar(4000) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT `PK_application_logs` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `data_sources` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Url` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
        `Type` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `Enabled` tinyint(1) NOT NULL,
        `LastSuccessAt` datetime(6) NULL,
        CONSTRAINT `PK_data_sources` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `fuels` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Code` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `SortOrder` int NOT NULL,
        CONSTRAINT `PK_fuels` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `stations` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `NormalizedKey` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Address` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `City` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Latitude` decimal(10,6) NOT NULL,
        `Longitude` decimal(10,6) NOT NULL,
        `ImageUrl` varchar(500) CHARACTER SET utf8mb4 NULL,
        `IsActive` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_stations` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `users` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `PasswordHash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Role` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `EmailConfirmed` tinyint(1) NOT NULL,
        `EmailConfirmationTokenHash` varchar(255) CHARACTER SET utf8mb4 NULL,
        `PasswordResetTokenHash` varchar(255) CHARACTER SET utf8mb4 NULL,
        `PasswordResetTokenExpiresAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        `LastLoginAt` datetime(6) NULL,
        CONSTRAINT `PK_users` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `parser_runs` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `SourceId` int NOT NULL,
        `StartedAt` datetime(6) NOT NULL,
        `FinishedAt` datetime(6) NULL,
        `Status` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `RecordsFound` int NOT NULL,
        `RecordsSaved` int NOT NULL,
        `Error` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Duration` time(6) NULL,
        CONSTRAINT `PK_parser_runs` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_parser_runs_data_sources_SourceId` FOREIGN KEY (`SourceId`) REFERENCES `data_sources` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `fuel_prices` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `StationId` int NOT NULL,
        `FuelId` int NOT NULL,
        `Price` decimal(6,2) NOT NULL,
        `Popularity` int NOT NULL,
        `Date` date NOT NULL,
        `SourceId` int NULL,
        `IsManual` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT `PK_fuel_prices` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_fuel_prices_data_sources_SourceId` FOREIGN KEY (`SourceId`) REFERENCES `data_sources` (`Id`) ON DELETE SET NULL,
        CONSTRAINT `FK_fuel_prices_fuels_FuelId` FOREIGN KEY (`FuelId`) REFERENCES `fuels` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_fuel_prices_stations_StationId` FOREIGN KEY (`StationId`) REFERENCES `stations` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `api_tokens` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `TokenHash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Scopes` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        `ExpiresAt` datetime(6) NULL,
        `RevokedAt` datetime(6) NULL,
        `CreatedByUserId` int NOT NULL,
        CONSTRAINT `PK_api_tokens` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_api_tokens_users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `comments` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `UserId` int NOT NULL,
        `StationId` int NOT NULL,
        `FuelId` int NULL,
        `Content` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_comments` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_comments_fuels_FuelId` FOREIGN KEY (`FuelId`) REFERENCES `fuels` (`Id`) ON DELETE SET NULL,
        CONSTRAINT `FK_comments_stations_StationId` FOREIGN KEY (`StationId`) REFERENCES `stations` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_comments_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE TABLE `subscriptions` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `UserId` int NOT NULL,
        `FuelId` int NOT NULL,
        `City` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Frequency` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT `PK_subscriptions` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_subscriptions_fuels_FuelId` FOREIGN KEY (`FuelId`) REFERENCES `fuels` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_subscriptions_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    INSERT INTO `data_sources` (`Id`, `Enabled`, `LastSuccessAt`, `Name`, `Type`, `Url`)
    VALUES (1, TRUE, NULL, 'Minfin Kharkiv', 'Html', 'https://index.minfin.com.ua/ua/markets/fuel/reg/harkovskaya/'),
    (2, FALSE, NULL, 'Open Fuel JSON', 'Json', 'https://example.com/fuel-prices.json');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    INSERT INTO `fuels` (`Id`, `Code`, `Name`, `SortOrder`)
    VALUES (1, 'a95plus', 'А 95+', 1),
    (2, 'a95', 'А 95', 2),
    (3, 'a92', 'А 92', 3),
    (4, 'diesel', 'ДП', 4),
    (5, 'gas', 'Газ', 5);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    INSERT INTO `stations` (`Id`, `Address`, `City`, `CreatedAt`, `ImageUrl`, `IsActive`, `Latitude`, `Longitude`, `Name`, `NormalizedKey`, `UpdatedAt`)
    VALUES (1, 'проспект Героїв Харкова, 142A', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.96685, 36.31701, 'AMIC', 'amic', NULL),
    (2, 'вулиця Григорія Сковороди, 85', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 50.01221, 36.25558, 'Marshal', 'marshal', NULL),
    (3, 'вулиця Клочківська, 98А', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 50.00509, 36.2189, 'Ovis', 'ovis', NULL),
    (4, 'Аерокосмічний проспект, 223', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.88417, 36.2919, 'Rodnik', 'rodnik', NULL),
    (5, 'вулиця Некрасова', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.94757, 36.1866, 'SUN OIL', 'sun-oil', NULL),
    (6, 'проспект Петра Григоренка', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.945, 36.31313, 'Shell', 'shell', NULL),
    (7, 'Аерокосмічний проспект, 127а', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.96461, 36.25994, 'U.GO', 'ugo', NULL),
    (8, 'вулиця Шевченка, 41', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.99609, 36.25002, 'WOG', 'wog', NULL),
    (9, 'проспект Байрона', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.9454, 36.33217, 'Авіас', 'avias', NULL),
    (10, 'вулиця Валентинівська, 2а', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 50.021, 36.31907, 'БРСМ-Нафта', 'brsm-nafta', NULL),
    (11, 'вулиця Клочківська, 44', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.99731, 36.22757, 'ОККО', 'okko', NULL),
    (12, 'вулиця Академіка Павлова', 'Харків', TIMESTAMP '2026-01-01 00:00:00', 'default-station.jpg', TRUE, 49.98969, 36.28772, 'Укрнафта', 'ukrnafta', NULL);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_api_tokens_CreatedByUserId` ON `api_tokens` (`CreatedByUserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_api_tokens_RevokedAt` ON `api_tokens` (`RevokedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_api_tokens_TokenHash` ON `api_tokens` (`TokenHash`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_application_logs_Category_CreatedAt` ON `application_logs` (`Category`, `CreatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_comments_FuelId` ON `comments` (`FuelId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_comments_StationId` ON `comments` (`StationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_comments_UserId` ON `comments` (`UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_data_sources_Name` ON `data_sources` (`Name`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_fuel_prices_FuelId_Date` ON `fuel_prices` (`FuelId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_fuel_prices_SourceId` ON `fuel_prices` (`SourceId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_fuel_prices_StationId_FuelId_Date` ON `fuel_prices` (`StationId`, `FuelId`, `Date`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_fuels_Code` ON `fuels` (`Code`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_fuels_Name` ON `fuels` (`Name`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_parser_runs_SourceId_StartedAt` ON `parser_runs` (`SourceId`, `StartedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_stations_City` ON `stations` (`City`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_stations_NormalizedKey_City` ON `stations` (`NormalizedKey`, `City`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE INDEX `IX_subscriptions_FuelId` ON `subscriptions` (`FuelId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_subscriptions_UserId_FuelId_City_Frequency` ON `subscriptions` (`UserId`, `FuelId`, `City`, `Frequency`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    CREATE UNIQUE INDEX `IX_users_Email` ON `users` (`Email`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260427212606_InitialCreate') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260427212606_InitialCreate', '9.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

