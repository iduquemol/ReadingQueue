export interface BookSuggestion {
  title: string;
  author: string;
  country: string;
  coverUrl: string | null | undefined;
  description?: string;
  year?: string | number;
  genre?: string; // 🚀 Garantizamos que viaje en la interfaz
}

const GenreCategoryMap: Record<string, string> = {
  // Específicos primero (orden importa: lo más específico al inicio)
  'latin american fiction': 'Novela latinoamericana',
  'latin american': 'Novela latinoamericana',
  'magical realism': 'Novela latinoamericana',
  'spanish fiction': 'Novela latinoamericana',
  'spanish': 'Novela latinoamericana',
  
  'classic literature': 'Novela clásica',
  'classics': 'Clásico',
  'antiquity': 'Clásico',
  
  'short stories': 'Cuentos',
  'short story': 'Cuentos',
  
  'essays': 'No ficción / ensayo',
  'essay': 'No ficción / ensayo',
  'nonfiction': 'No ficción / ensayo',
  'non-fiction': 'No ficción / ensayo',
  'biography': 'No ficción / ensayo',
  'memoir': 'No ficción / ensayo',
  'history': 'No ficción / ensayo',
  'philosophy': 'No ficción / ensayo',
  
  'poetry': 'Poesía',
  
  // Genéricos al final (catch-all)
  'fiction': 'Novela contemporánea',
  'literary fiction': 'Novela contemporánea',
  'science fiction': 'Novela contemporánea',
  'speculative fiction': 'Novela contemporánea',
  'crime': 'Novela contemporánea',
  'mystery': 'Novela contemporánea',
  'thriller': 'Novela contemporánea',
  'fantasy': 'Novela contemporánea',
  'young adult': 'Novela contemporánea',
  'children': 'Novela contemporánea',
  'drama': 'Novela contemporánea',
};

const normalizeCategory = (value: string): string =>
  value
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9\s]/g, '')
    .replace(/\s+/g, ' ');

const mapCategoryToGenre = (rawCategory?: string): string | undefined => {
  if (!rawCategory) return undefined;

  const candidates = rawCategory
    .split(/[,;\/\|\\-–—]/)
    .map((piece) => normalizeCategory(piece))
    .filter(Boolean);

  // Para cada candidato, encontrar el match MÁS ESPECÍFICO (la clave más larga)
  for (const candidate of candidates) {
    let bestMatch: string | undefined;
    let bestMatchLength = 0;

    // Buscar la clave más larga del mapa que esté contenida en el candidato
    for (const mapKey in GenreCategoryMap) {
      if (candidate.includes(mapKey) && mapKey.length > bestMatchLength) {
        bestMatch = mapKey;
        bestMatchLength = mapKey.length;
      }
    }

    if (bestMatch) {
      return GenreCategoryMap[bestMatch];
    }
  }

  return undefined;
};

export async function lookupBook(title: string): Promise<BookSuggestion[]> {
  if (!title.trim()) return [];

  // 1. INTENTO CON GOOGLE BOOKS (Limpia sin parámetros extras de key defectuosos)
  try {
    const response = await fetch(
      `https://www.googleapis.com/books/v1/volumes?q=intitle:${encodeURIComponent(title)}&maxResults=5`
    );
    
    if (response.ok) {
      const data = await response.json();
      if (data.items && data.items.length > 0) {
        return data.items.map((item: any) => {
          const rawGenres = Array.isArray(item.volumeInfo?.categories)
            ? item.volumeInfo.categories.map((c: any) => String(c ?? '').trim()).filter(Boolean)
            : [];

          const genre = rawGenres
            .map((value: string) => mapCategoryToGenre(value))
            .find((mapped: string | undefined): mapped is string => typeof mapped === 'string' && mapped.length > 0)
            ?? mapCategoryToGenre(rawGenres[0]);

          return {
            title: item.volumeInfo.title,
            author: item.volumeInfo.authors?.[0] ?? 'Autor Desconocido',
            country: '',
            coverUrl: item.volumeInfo.imageLinks?.thumbnail ?? null,
            description: item.volumeInfo.description ?? '',
            year: item.volumeInfo.publishedDate?.slice(0, 4) ?? '',
            genre,
          };
        });
      }
    }
  } catch (error) {
    console.warn("Google Books falló o dio 429. Usando Open Library...", error);
  }

  // 2. RESPALDO CON OPEN LIBRARY (Para cuando Google se bloquee)
  try {
    const olResponse = await fetch(
      `https://openlibrary.org/search.json?title=${encodeURIComponent(title)}&limit=5`
    );
    
    if (olResponse.ok) {
      const olData = await olResponse.json();
      if (olData.docs && olData.docs.length > 0) {
        // Queremos intentar recuperar subjects incluso si search.json no los incluye
        const results = await Promise.all(olData.docs.map(async (doc: any) => {
          const subjectKeys = [
            'subject', 'subjects', 'subject_facet',
            'subject_time', 'subject_times',
            'subject_place', 'subject_places',
            'subject_person', 'subject_people',
            'subject_topic'
          ];

          let rawGenre: string | undefined = undefined;
          for (const key of subjectKeys) {
            const v = doc[key];
            if (!v) continue;
            if (Array.isArray(v) && v.length > 0) {
              const cand = String(v.find((x: any) => typeof x === 'string' && String(x).trim()) ?? v[0]);
              if (cand && cand.trim()) { rawGenre = cand.trim(); break; }
            }
            if (typeof v === 'string' && v.trim()) { rawGenre = v.trim(); break; }
          }

          if (!rawGenre && doc.subject && typeof doc.subject === 'string') {
            const parts = doc.subject.split(/[,;\/\|\\-–—]/).map((s: string) => s.trim()).filter(Boolean);
            if (parts.length > 0) rawGenre = parts[0];
          }

          // Si todavía no hay género, intentamos llamar al endpoint de la obra (/works)
          // que suele contener un array `subjects` más completo.
          if (!rawGenre && doc.key) {
            try {
              const workResp = await fetch(`https://openlibrary.org${doc.key}.json`);
              if (workResp.ok) {
                const workData = await workResp.json();
                if (Array.isArray(workData.subjects) && workData.subjects.length > 0) {
                  rawGenre = String(workData.subjects[0]).trim();
                }
              }
            } catch (e) {
              // ignore network errors for the fallback
            }
          }

          // Como última opción, intentar obtener la primera edición y leer su subject
          if (!rawGenre && Array.isArray(doc.edition_key) && doc.edition_key.length > 0) {
            try {
              const editionId = doc.edition_key[0];
              const edResp = await fetch(`https://openlibrary.org/books/${editionId}.json`);
              if (edResp.ok) {
                const edData = await edResp.json();
                if (Array.isArray(edData.subjects) && edData.subjects.length > 0) {
                  rawGenre = String(edData.subjects[0]).trim();
                }
              }
            } catch (e) {
              // ignore
            }
          }

          return {
            title: doc.title,
            author: doc.author_name?.[0] ?? 'Autor Desconocido',
            country: '',
            coverUrl: doc.cover_i ? `https://covers.openlibrary.org/b/id/${doc.cover_i}-M.jpg` : null,
            year: doc.first_publish_year ?? '',
            genre: mapCategoryToGenre(rawGenre) ?? rawGenre,
          };
        }));

        return results;
      }
    }
  } catch (olError) {
    console.error("Ambas APIs fallaron por completo:", olError);
  }

  return [];
}