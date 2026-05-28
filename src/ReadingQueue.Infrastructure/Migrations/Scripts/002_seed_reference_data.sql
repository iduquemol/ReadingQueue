-- ============================================================
-- Generos (7 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'No ficcion / ensayo')
    INSERT INTO Genres (Name) VALUES ('No ficcion / ensayo');
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Clasico')
    INSERT INTO Genres (Name) VALUES ('Clasico');
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Novela contemporanea')
    INSERT INTO Genres (Name) VALUES ('Novela contemporanea');
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Novela latinoamericana')
    INSERT INTO Genres (Name) VALUES ('Novela latinoamericana');
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Cuentos')
    INSERT INTO Genres (Name) VALUES ('Cuentos');
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Novela clasica')
    INSERT INTO Genres (Name) VALUES ('Novela clasica');
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Poesia')
    INSERT INTO Genres (Name) VALUES ('Poesia');

-- ============================================================
-- Niveles de energia mental (5 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = 'Baja - cualquier momento')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('Baja - cualquier momento', 1);
IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = 'Media - tarde tranquila')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('Media - tarde tranquila', 2);
IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = 'Media-alta - fin de semana')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('Media-alta - fin de semana', 3);
IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = 'Alta - concentracion')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('Alta - concentracion', 4);
IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = 'Maxima - modo lector')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('Maxima - modo lector', 5);

-- ============================================================
-- Animos recomendados (7 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Analitico / quiero aprender algo')
    INSERT INTO Moods (Name) VALUES ('Analitico / quiero aprender algo');
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Solemne / quiero leer algo grande')
    INSERT INTO Moods (Name) VALUES ('Solemne / quiero leer algo grande');
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Curioso / quiero algo fresco')
    INSERT INTO Moods (Name) VALUES ('Curioso / quiero algo fresco');
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Identidad / quiero leer en espanol')
    INSERT INTO Moods (Name) VALUES ('Identidad / quiero leer en espanol');
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Cansado / quiero entrar y salir')
    INSERT INTO Moods (Name) VALUES ('Cansado / quiero entrar y salir');
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Contemplativo / quiero algo que dure')
    INSERT INTO Moods (Name) VALUES ('Contemplativo / quiero algo que dure');
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Sensible / quiero pocas palabras')
    INSERT INTO Moods (Name) VALUES ('Sensible / quiero pocas palabras');

-- ============================================================
-- Categorias de rotacion (10 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Ficción literaria y narrativa general')
    INSERT INTO RotationCategories (Name) VALUES ('Ficción literaria y narrativa general');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Ciencia ficción')
    INSERT INTO RotationCategories (Name) VALUES ('Ciencia ficción');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Fantasía')
    INSERT INTO RotationCategories (Name) VALUES ('Fantasía');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Terror y horror')
    INSERT INTO RotationCategories (Name) VALUES ('Terror y horror');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Misterio y crimen')
    INSERT INTO RotationCategories (Name) VALUES ('Misterio y crimen');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Romance')
    INSERT INTO RotationCategories (Name) VALUES ('Romance');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'No ficción')
    INSERT INTO RotationCategories (Name) VALUES ('No ficción');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Poesía')
    INSERT INTO RotationCategories (Name) VALUES ('Poesía');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Teatro')
    INSERT INTO RotationCategories (Name) VALUES ('Teatro');
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Cómic y novela gráfica')
    INSERT INTO RotationCategories (Name) VALUES ('Cómic y novela gráfica');
