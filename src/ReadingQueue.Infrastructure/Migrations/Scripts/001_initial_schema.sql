-- ============================================================
-- Usuarios del sistema
-- ============================================================
CREATE TABLE Users (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Email        NVARCHAR(256)  NOT NULL,
    PasswordHash NVARCHAR(512)  NOT NULL,
    DisplayName  NVARCHAR(200)  NOT NULL,
    CreatedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    IsActive     BIT            NOT NULL DEFAULT 1,

    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

-- ============================================================
-- Biblioteca de libros por usuario
-- ============================================================
CREATE TABLE Books (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT             NOT NULL REFERENCES Users(Id),
    Title            NVARCHAR(500)   NOT NULL,
    Author           NVARCHAR(300)   NOT NULL,
    Genre            NVARCHAR(100)   NOT NULL,
    Country          NVARCHAR(100)   NOT NULL,
    WhyRead          NVARCHAR(1000)  NULL,
    Priority         TINYINT         NOT NULL DEFAULT 3,
    MentalEnergy     NVARCHAR(100)   NOT NULL,
    RecommendedMood  NVARCHAR(200)   NOT NULL,
    RotationCategory NVARCHAR(100)   NOT NULL,
    IsRead           BIT             NOT NULL DEFAULT 0,
    ReadAt           DATETIME2       NULL,
    Notes            NVARCHAR(2000)  NULL,
    CreatedAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT CK_Books_Priority CHECK (Priority BETWEEN 1 AND 5)
);

CREATE INDEX IX_Books_UserId        ON Books(UserId);
CREATE INDEX IX_Books_UserId_Genre  ON Books(UserId, Genre);
CREATE INDEX IX_Books_UserId_IsRead ON Books(UserId, IsRead);

-- ============================================================
-- Cola de lectura activa por usuario
-- ============================================================
CREATE TABLE ReadingQueue (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    UserId   INT           NOT NULL REFERENCES Users(Id),
    BookId   INT           NOT NULL REFERENCES Books(Id),
    Position INT           NOT NULL,
    AddedAt  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    Source   NVARCHAR(50)  NOT NULL DEFAULT 'Manual',

    CONSTRAINT UQ_Queue_UserBook UNIQUE (UserId, BookId)
);

CREATE INDEX IX_Queue_UserId ON ReadingQueue(UserId, Position);

-- ============================================================
-- Sugerencias generadas por Claude
-- ============================================================
CREATE TABLE AISuggestions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT             NOT NULL REFERENCES Users(Id),
    BookId      INT             NOT NULL REFERENCES Books(Id),
    Reasoning   NVARCHAR(2000)  NOT NULL,
    Score       DECIMAL(5,2)    NOT NULL,
    GeneratedAt DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    WasAccepted BIT             NULL
);

-- ============================================================
-- Refresh tokens de sesion
-- ============================================================
CREATE TABLE RefreshTokens (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT            NOT NULL REFERENCES Users(Id),
    Token     NVARCHAR(512)  NOT NULL,
    ExpiresAt DATETIME2      NOT NULL,
    CreatedAt DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    IsRevoked BIT            NOT NULL DEFAULT 0,

    CONSTRAINT UQ_RefreshTokens_Token UNIQUE (Token)
);

-- ============================================================
-- Tablas de referencia (enumeraciones — solo lectura desde API)
-- ============================================================
CREATE TABLE Genres (
    Name NVARCHAR(100) NOT NULL PRIMARY KEY
);

CREATE TABLE MentalEnergyLevels (
    Name      NVARCHAR(100) NOT NULL PRIMARY KEY,
    SortOrder TINYINT       NOT NULL
);

CREATE TABLE Moods (
    Name NVARCHAR(200) NOT NULL PRIMARY KEY
);

CREATE TABLE RotationCategories (
    Name NVARCHAR(100) NOT NULL PRIMARY KEY
);
