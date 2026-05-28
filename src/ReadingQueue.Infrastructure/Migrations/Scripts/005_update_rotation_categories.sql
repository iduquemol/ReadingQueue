-- ============================================================
-- Actualiza las categorías de rotación existentes para mantener solo las nuevas
-- ============================================================

UPDATE Books
SET RotationCategory = CASE
    WHEN RotationCategory IN (
        'Clasico',
        'Novela grande',
        'Contemporaneo latinoamericano o raro',
        'Libro corto o cuentos'
    ) THEN 'Ficción literaria y narrativa general'
    WHEN RotationCategory = 'Ensayo / no ficcion' THEN 'No ficción'
    ELSE RotationCategory
END
WHERE RotationCategory IN (
    'Clasico',
    'Novela grande',
    'Contemporaneo latinoamericano o raro',
    'Libro corto o cuentos',
    'Ensayo / no ficcion'
);

DELETE FROM RotationCategories
WHERE Name NOT IN (
    'Ficción literaria y narrativa general',
    'Ciencia ficción',
    'Fantasía',
    'Terror y horror',
    'Misterio y crimen',
    'Romance',
    'No ficción',
    'Poesía',
    'Teatro',
    'Cómic y novela gráfica'
);

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
