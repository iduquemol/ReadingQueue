-- ============================================================
-- Crear tabla Subgenres y columna Subgenre en Books
-- ============================================================

-- Crear tabla de subgéneros
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Subgenres')
BEGIN
    CREATE TABLE Subgenres (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Genre NVARCHAR(100) NOT NULL REFERENCES Genres(Name)
    );
END

-- Añadir columna Subgenre a Books (nullable por compatibilidad hacia atrás)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Subgenre' AND Object_ID = Object_ID(N'Books'))
BEGIN
    ALTER TABLE Books ADD Subgenre NVARCHAR(200) NULL;
END

-- Datos de ejemplo para subgéneros
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Realismo mágico' AND Genre = 'Novela latinoamericana')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Realismo mágico', 'Novela latinoamericana');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Novela de la estructura (Boom)' AND Genre = 'Novela latinoamericana')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Novela de la estructura (Boom)', 'Novela latinoamericana');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Literatura indígena / regional' AND Genre = 'Novela latinoamericana')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Literatura indígena / regional', 'Novela latinoamericana');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Novela de la violencia' AND Genre = 'Novela latinoamericana')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Novela de la violencia', 'Novela latinoamericana');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Desarrollo personal / Hábitos' AND Genre = 'No ficcion / ensayo')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Desarrollo personal / Hábitos', 'No ficcion / ensayo');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Biografías / Memorias' AND Genre = 'No ficcion / ensayo')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Biografías / Memorias', 'No ficcion / ensayo');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Productividad / Negocios' AND Genre = 'No ficcion / ensayo')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Productividad / Negocios', 'No ficcion / ensayo');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Filosofía / Historia' AND Genre = 'No ficcion / ensayo')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Filosofía / Historia', 'No ficcion / ensayo');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Novela gótica' AND Genre = 'Novela clasica')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Novela gótica', 'Novela clasica');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Realismo del Siglo XIX' AND Genre = 'Novela clasica')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Realismo del Siglo XIX', 'Novela clasica');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Caballería / Épica' AND Genre = 'Novela clasica')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Caballería / Épica', 'Novela clasica');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Novela epistolar' AND Genre = 'Novela clasica')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Novela epistolar', 'Novela clasica');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Thriller / Suspenso' AND Genre = 'Novela contemporanea')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Thriller / Suspenso', 'Novela contemporanea');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Ciencia ficción / Distopía' AND Genre = 'Novela contemporanea')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Ciencia ficción / Distopía', 'Novela contemporanea');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Fantasía moderna' AND Genre = 'Novela contemporanea')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Fantasía moderna', 'Novela contemporanea');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Novela histórica' AND Genre = 'Novela contemporanea')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Novela histórica', 'Novela contemporanea');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Antología de terror' AND Genre = 'Cuentos')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Antología de terror', 'Cuentos');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Cuento fantástico' AND Genre = 'Cuentos')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Cuento fantástico', 'Cuentos');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Realismo sucio' AND Genre = 'Cuentos')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Realismo sucio', 'Cuentos');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Microrelato' AND Genre = 'Cuentos')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Microrelato', 'Cuentos');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Antología poética' AND Genre = 'Poesia')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Antología poética', 'Poesia');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Poesía vanguardista' AND Genre = 'Poesia')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Poesía vanguardista', 'Poesia');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Lírica clásica' AND Genre = 'Poesia')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Lírica clásica', 'Poesia');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Poesía contemporánea' AND Genre = 'Poesia')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Poesía contemporánea', 'Poesia');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Tragedia griega / Teatro clásico' AND Genre = 'Clasico')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Tragedia griega / Teatro clásico', 'Clasico');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Epopeya antigua' AND Genre = 'Clasico')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Epopeya antigua', 'Clasico');
IF NOT EXISTS (SELECT 1 FROM Subgenres WHERE Name = 'Filosofía clásica' AND Genre = 'Clasico')
    INSERT INTO Subgenres (Name, Genre) VALUES ('Filosofía clásica', 'Clasico');

-- (Opcional) Índice para consultas por usuario y género/subgénero
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Books_UserId_Genre_Subgenre')
BEGIN
    CREATE INDEX IX_Books_UserId_Genre_Subgenre ON Books(UserId, Genre, Subgenre);
END
