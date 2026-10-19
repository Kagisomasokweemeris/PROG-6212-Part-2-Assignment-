/* =====================================================================
   RaceDay - SQL Server database script (Part 1, used by the Part 2 API)
   Matches the ERD (RaceDay_ERD.png) and the EF Core model in src/RaceDay.Api.
   Run in SSMS against a clean SQL Server instance.
   Seed login for every sample user: Password123!
   ===================================================================== */
IF DB_ID(N'RaceDayDb') IS NULL
    CREATE DATABASE RaceDayDb;
GO
USE RaceDayDb;
GO

/* ---------- Drop in dependency order so the script can be re-run ---------- */
DROP TABLE IF EXISTS dbo.Results;
DROP TABLE IF EXISTS dbo.Enrolments;
DROP TABLE IF EXISTS dbo.Categories;
DROP TABLE IF EXISTS dbo.Events;
DROP TABLE IF EXISTS dbo.Users;
DROP TABLE IF EXISTS dbo.Roles;
GO

/* ---------- Tables ---------- */
CREATE TABLE dbo.Roles (
    RoleId    INT IDENTITY(1,1) NOT NULL,
    RoleName  NVARCHAR(30)      NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);

CREATE TABLE dbo.Users (
    UserId        INT IDENTITY(1,1) NOT NULL,
    FirstName     NVARCHAR(100)     NOT NULL,
    LastName      NVARCHAR(100)     NOT NULL,
    Email         NVARCHAR(200)     NOT NULL,
    PasswordHash  NVARCHAR(500)     NOT NULL,   -- PBKDF2 hash, never the plain password
    DateOfBirth   DATETIME2         NOT NULL,
    PhoneNumber   NVARCHAR(30)      NULL,
    RoleId        INT               NOT NULL,
    CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);

CREATE TABLE dbo.Events (
    EventId      INT IDENTITY(1,1) NOT NULL,
    OrganiserId  INT               NOT NULL,
    Name         NVARCHAR(200)     NOT NULL,
    Description  NVARCHAR(2000)    NOT NULL,
    EventDate    DATETIME2         NOT NULL,
    Location     NVARCHAR(200)     NOT NULL,
    DistanceKm   DECIMAL(6,2)      NOT NULL,
    EventType    NVARCHAR(10)      NOT NULL,
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run','Walk','Cycle'))
);

CREATE TABLE dbo.Categories (
    CategoryId  INT IDENTITY(1,1) NOT NULL,
    EventId     INT               NOT NULL,
    Name        NVARCHAR(100)     NOT NULL,
    MinAge      INT               NULL,
    MaxAge      INT               NULL,
    DistanceKm  DECIMAL(6,2)      NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId) ON DELETE CASCADE
);

CREATE TABLE dbo.Enrolments (
    EnrolmentId  INT IDENTITY(1,1) NOT NULL,
    UserId       INT               NOT NULL,
    EventId      INT               NOT NULL,
    CategoryId   INT               NOT NULL,
    EnrolledAt   DATETIME2         NOT NULL CONSTRAINT DF_Enrolments_EnrolledAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Enrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT UQ_Enrolments_User_Event UNIQUE (UserId, EventId),   -- one entry per participant per event
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId) ON DELETE CASCADE,
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
);

CREATE TABLE dbo.Results (
    ResultId        INT IDENTITY(1,1) NOT NULL,
    EnrolmentId     INT               NOT NULL,
    FinishTime      TIME(7)           NOT NULL,
    FinishPosition  INT               NOT NULL,
    CapturedAt      DATETIME2         NOT NULL CONSTRAINT DF_Results_CapturedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId),           -- one result per enrolment (1:1)
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments(EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT CK_Results_Position CHECK (FinishPosition >= 1)
);
GO

/* ---------- Seed data ---------- */
SET IDENTITY_INSERT dbo.Roles ON;
INSERT INTO dbo.Roles (RoleId, RoleName) VALUES (1, N'Organiser'), (2, N'Participant');
SET IDENTITY_INSERT dbo.Roles OFF;

-- Users 1-2 are Organisers, 3-4 are Participants. Password for all: Password123!
SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, FirstName, LastName, Email, PasswordHash, DateOfBirth, PhoneNumber, RoleId) VALUES
(1, N'Nomsa',  N'Khumalo', N'nomsa.khumalo@raceday.example',  N'PBKDF2$100000$7e9LnDR0PCmBJqnOOVb1gw==$mdBofDA6sthUi9NMwS86dbonfrG0F2DLjVQyXBis2so=', '1984-03-12', N'0821112233', 1),
(2, N'Pieter', N'van Wyk', N'pieter.vanwyk@raceday.example', N'PBKDF2$100000$iN5c6i4MxW6660zLXDQ0Vw==$fifQKkeD9S/JNgDREhVCIWoVVe4MhRdpYQ9NvDK+3AM=', '1979-09-30', N'0834445566', 1),
(3, N'Lerato', N'Molefe',  N'lerato.molefe@raceday.example',  N'PBKDF2$100000$F5v19GEjCHhwXl6ivMvZZw==$0vLtYQvWgW6xm+yzcNxWu7aMoxQUROzo2oCMV2du5Sk=', '2001-07-04', N'0719998877', 2),
(4, N'Johan',  N'Botha',   N'johan.botha@raceday.example',    N'PBKDF2$100000$Dt1/Mg6wzdc/rvAAnpu6TA==$H35RpQyM/P6BURdE5WhKgph5VV331k0eZj5graxIJt4=', '1972-11-23', N'0762223344', 2);
SET IDENTITY_INSERT dbo.Users OFF;

SET IDENTITY_INSERT dbo.Events ON;
INSERT INTO dbo.Events (EventId, OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType) VALUES
(1, 1, N'Table Mountain Sunrise Fun Run',   N'A scenic road run along the Atlantic Seaboard, starting at sunrise.',        '2026-11-21T06:00:00', N'Sea Point Promenade, Cape Town',  10.00, N'Run'),
(2, 1, N'Durban Beachfront Charity Walk',   N'A relaxed community walk along the Golden Mile in aid of local charities.',  '2026-12-06T07:30:00', N'Golden Mile, Durban',              5.00, N'Walk'),
(3, 2, N'Winelands Cycle Classic',          N'A rolling road cycle through the Stellenbosch winelands.',                   '2027-01-17T06:30:00', N'Stellenbosch, Western Cape',      60.00, N'Cycle');
SET IDENTITY_INSERT dbo.Events OFF;

SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories (CategoryId, EventId, Name, MinAge, MaxAge, DistanceKm) VALUES
(1, 1, N'Under 20',      0, 19,   10.00),
(2, 1, N'Senior',       20, 49,   10.00),
(3, 1, N'Veteran',      50, NULL, 10.00),
(4, 2, N'5km Family Walk', NULL, NULL, 5.00),
(5, 2, N'5km Seniors Walk', 60, NULL, 5.00),
(6, 3, N'60km Open',    18, NULL, 60.00),
(7, 3, N'30km Social',  NULL, NULL, 30.00);
SET IDENTITY_INSERT dbo.Categories OFF;

SET IDENTITY_INSERT dbo.Enrolments ON;
INSERT INTO dbo.Enrolments (EnrolmentId, UserId, EventId, CategoryId) VALUES
(1, 3, 1, 2),   -- Lerato  -> Table Mountain Fun Run, Senior
(2, 3, 2, 4),   -- Lerato  -> Durban Charity Walk, Family Walk
(3, 4, 1, 3),   -- Johan   -> Table Mountain Fun Run, Veteran
(4, 4, 3, 6);   -- Johan   -> Winelands Cycle Classic, 60km Open
SET IDENTITY_INSERT dbo.Enrolments OFF;
GO

/* Quick check: one row per table */
SELECT 'Roles' AS TableName, COUNT(*) AS TotalRows FROM dbo.Roles
UNION ALL SELECT 'Users', COUNT(*) FROM dbo.Users
UNION ALL SELECT 'Events', COUNT(*) FROM dbo.Events
UNION ALL SELECT 'Categories', COUNT(*) FROM dbo.Categories
UNION ALL SELECT 'Enrolments', COUNT(*) FROM dbo.Enrolments
UNION ALL SELECT 'Results', COUNT(*) FROM dbo.Results;
GO
