import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { createBookSchema, type CreateBookFormValues } from '@/lib/schemas/book.schemas'
import { useSubgenres } from '@/hooks/useReferenceData'
import { Button } from '@/components/ui/button'
import { Input }  from '@/components/ui/input'
import { Label }  from '@/components/ui/label'
import { lookupBook, type BookSuggestion } from '@/api/bookLookupApi'; 

const COUNTRIES_LIST = [
  "Argentina", "Bolivia", "Brasil", "Chile", "Colombia", "Costa Rica", 
  "Cuba", "Ecuador", "El Salvador", "España", "Estados Unidos", 
  "Guatemala", "Honduras", "México", "Nicaragua", "Panamá", 
  "Paraguay", "Perú", "Puerto Rico", "República Dominicana", 
  "Uruguay", "Venezuela"
];

interface Props {
  defaultValues?:     Partial<CreateBookFormValues>
  onSubmit:           (data: CreateBookFormValues) => void
  isPending?:         boolean
  genres:             string[]
  mentalEnergyLevels: string[]
  moods:              string[]
  rotationCategories: string[]
}

export function BookForm({
  defaultValues, onSubmit, isPending = false,
  genres, mentalEnergyLevels, moods, rotationCategories,
}: Props) {
  const { register, handleSubmit, watch, setValue, getValues, formState: { errors }, trigger } = useForm<CreateBookFormValues>({
    resolver:      zodResolver(createBookSchema),
    defaultValues: { priority: 3, ...defaultValues },
  })

  const selectedGenre = watch('genre')
  const { data: subgenres = [], isFetching: isLoadingSubgenres } = useSubgenres(selectedGenre)

  // Estados para el buscador predictivo
  const [searchQuery, setSearchQuery] = useState('');
  const [suggestions, setSuggestions] = useState<BookSuggestion[]>([]);
  const [isLoadingSearch, setIsLoadingSearch] = useState(false);
  const [tempGenreOption, setTempGenreOption] = useState<string | null>(null);

  // Ejecuta la consulta a las APIs 
  const handleSearchBooks = async () => {
    if (!searchQuery.trim()) return;
    setIsLoadingSearch(true);
    try {
      const results = await lookupBook(searchQuery);
      setSuggestions(results);
    } catch (error) {
      console.error("Error al buscar metadatos del libro:", error);
    } finally {
      setIsLoadingSearch(false);
    }
  };

 const handleSelectSuggestion = (book: BookSuggestion) => {
    console.log("=== 🔍 INICIO DEL RASTREO ===");
    console.log("1. Objeto completo que llegó de la API:", book);
    
    // Rellenamos título y autor
    setValue('title', book.title);
    setValue('author', book.author);
    console.log("2. Título y Autor asignados en el formulario.");

    if (book.genre) {
      console.log("3. El género original de la API es:", book.genre);
      
      const transformed = transformApiGenre(book.genre);
      const exactMatch = findExactGenreMatch(book.genre);
      const transformedMatch = transformed ? findGenreOptionByNormalizedValue(transformed) : '';
      const genreToSet = exactMatch || transformedMatch || transformed;

      console.log("4. El transformador devolvió este texto exacto:", `"${transformed}"`);
      console.log("5. Coincidencia exacta con opciones disponibles:", `"${exactMatch}"`);
      console.log("6. Coincidencia normalizada para el texto transformado:", `"${transformedMatch}"`);

      if (genreToSet) {
        setValue('genre', genreToSet);
        // Si el género no está en la lista de opciones, lo guardamos como opción temporal
        if (!genres.includes(genreToSet)) setTempGenreOption(genreToSet);
        else setTempGenreOption(null);
        console.log("7. ¡Se ejecutó setValue('genre', '" + genreToSet + "')!");
      } else {
        console.warn("⚠️ No se encontró un género válido dentro de las opciones disponibles. El usuario deberá escogerlo manualmente.");
      }
    } else {
      console.warn("⚠️ El objeto 'book' no traía la propiedad 'genre'. Venía undefined o vacía.");
    }

    // Volvemos a validar para limpiar los mensajes en rojo
    trigger(['title', 'author', 'genre']);
    
    // Leemos el valor actual del formulario un milisegundo después para ver si se guardó
    setTimeout(() => {
      console.log("7. Valor final real en el input de género:", getValues('genre'));
      console.log("=== 👁️ FIN DEL RASTREO ===");
    }, 100);

    setSuggestions([]);
    setSearchQuery('');
  };

  useEffect(() => {
    if (selectedGenre === '') {
      setValue('subgenre', '');
    }
    // Si el usuario cambió manualmente el género y ya no coincide con la opción temporal,
    // limpiamos la opción temporal para que no vuelva a mostrarse.
    if (tempGenreOption && selectedGenre !== tempGenreOption) {
      setTempGenreOption(null);
    }
  }, [selectedGenre, setValue, tempGenreOption]);

  const normalizeGenreText = (value: string) =>
    value
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9\s\/\-]/g, '')
      .replace(/\s+/g, ' ');

  const findGenreOptionByNormalizedValue = (value: string): string => {
    const normalizedValue = normalizeGenreText(value);
    return genres.find((option) => normalizeGenreText(option) === normalizedValue) ?? '';
  };

  const findExactGenreMatch = (apiGenre: string): string => {
    return findGenreOptionByNormalizedValue(apiGenre);
  };

  const transformApiGenre = (apiGenre: string | undefined): string => {
    if (!apiGenre) return '';

    const genre = normalizeGenreText(apiGenre);

    // Lista de patrones por especificidad (puntuación)
    // Mayor puntuación = mayor especificidad
    const patterns: Array<{ score: number; keywords: string[]; result: string }> = [
      // NIVEL 3: Muy específico (3+ keywords)
      { score: 30, keywords: ['spanish', 'fiction'], result: 'Novela latinoamericana' },
      { score: 30, keywords: ['latin', 'american', 'fiction'], result: 'Novela latinoamericana' },
      
      // NIVEL 2: Específico (2 keywords)
      { score: 20, keywords: ['latin', 'american'], result: 'Novela latinoamericana' },
      { score: 20, keywords: ['magical', 'realism'], result: 'Novela latinoamericana' },
      { score: 20, keywords: ['classic', 'literature'], result: 'Novela clásica' },
      { score: 20, keywords: ['short', 'stories'], result: 'Cuentos' },
      { score: 20, keywords: ['short', 'story'], result: 'Cuentos' },
      { score: 20, keywords: ['non', 'fiction'], result: 'No ficción / ensayo' },
      { score: 20, keywords: ['self', 'help'], result: 'No ficción / ensayo' },
      { score: 20, keywords: ['science', 'fiction'], result: 'Novela contemporánea' },
      { score: 20, keywords: ['speculative', 'fiction'], result: 'Novela contemporánea' },
      { score: 20, keywords: ['young', 'adult'], result: 'Novela contemporánea' },
      { score: 20, keywords: ['historical', 'fiction'], result: 'Novela contemporánea' },
      
      // NIVEL 1: General (1 keyword)
      { score: 10, keywords: ['spanish'], result: 'Novela latinoamericana' },
      { score: 10, keywords: ['latinoamericana'], result: 'Novela latinoamericana' },
      { score: 10, keywords: ['latinoamerican'], result: 'Novela latinoamericana' },
      { score: 10, keywords: ['clasico'], result: 'Novela clásica' },
      { score: 10, keywords: ['novela', 'clasica'], result: 'Novela clásica' },
      { score: 10, keywords: ['cuentos'], result: 'Cuentos' },
      { score: 10, keywords: ['relatos'], result: 'Cuentos' },
      { score: 10, keywords: ['history'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['historia'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['essay'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['ensayo'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['biography'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['memoir'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['philosophy'], result: 'No ficción / ensayo' },
      { score: 10, keywords: ['poetry'], result: 'Poesía' },
      { score: 10, keywords: ['poesia'], result: 'Poesía' },
      { score: 10, keywords: ['poesía'], result: 'Poesía' },
      { score: 10, keywords: ['poems'], result: 'Poesía' },
      { score: 5, keywords: ['fiction'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['novel'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['literary'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['crime'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['mystery'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['thriller'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['fantasy'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['children'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['drama'], result: 'Novela contemporánea' },
      { score: 5, keywords: ['general'], result: 'Novela contemporánea' },
    ];

    // Encontrar el patrón con mayor puntuación que coincida
    let bestMatch: { result: string; score: number } | null = null;

    for (const pattern of patterns) {
      const matchesAllKeywords = pattern.keywords.every((keyword) => genre.includes(keyword));
      if (matchesAllKeywords && pattern.score > (bestMatch?.score ?? -1)) {
        bestMatch = { result: pattern.result, score: pattern.score };
      }
    }

    return bestMatch?.result ?? '';
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
      {/* 🔍 Buscador Predictivo (Google Books & Open Library Fallback) */}
      <div className="space-y-2 p-4 bg-gray-50 border border-gray-200 rounded-lg relative dark:bg-zinc-900 dark:border-zinc-850">
        <div className="flex gap-2">
          <Input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Escribe el nombre del libro que buscas..."
            className="flex-1 bg-white text-black dark:bg-zinc-800 dark:text-white"
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault(); // Evita que se envíe el formulario completo
                handleSearchBooks();
              }
            }}
          />
          <Button
            type="button"
            onClick={handleSearchBooks}
            disabled={isLoadingSearch}
            className="px-4"
          >
            {isLoadingSearch ? 'Buscando...' : 'Buscar'}
          </Button>
        </div>

        {/* Menú desplegable flotante de las sugerencias */}
        {suggestions.length > 0 && (
          <div className="absolute left-0 right-0 top-full mt-1 z-50 bg-white border border-gray-300 rounded-md shadow-xl max-h-64 overflow-y-auto p-1 divide-y divide-gray-100 text-black">
            {suggestions.map((book, index) => (
              <button
                key={index}
                type="button"
                onClick={() => handleSelectSuggestion(book)}
                className="w-full flex items-center gap-3 p-2 hover:bg-blue-50 transition-colors rounded text-left focus:outline-none focus:bg-blue-50"
              >
                {book.coverUrl ? (
                  <img 
                    src={book.coverUrl} 
                    alt={book.title} 
                    className="w-9 h-12 object-cover rounded shadow-sm flex-shrink-0" 
                  />
                ) : (
                  <div className="w-9 h-12 bg-gray-200 rounded flex items-center justify-center text-[10px] text-gray-400 font-medium flex-shrink-0">
                    S/P
                  </div>
                )}
                <div className="overflow-hidden">
                  <p className="font-semibold text-xs text-gray-900 truncate">{book.title}</p>
                  <p className="text-[11px] text-gray-500 truncate">
                    {book.author || 'Autor desconocido'} {book.year ? `• (${book.year})` : ''}
                  </p>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="relative flex py-2 items-center">
        <div className="flex-grow border-t border-gray-200 dark:border-zinc-800"></div>
        <span className="flex-shrink mx-4 text-xs text-gray-400">O ingresa manualmente:</span>
        <div className="flex-grow border-t border-gray-200 dark:border-zinc-800"></div>
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-title">Título *</Label>
        <Input id="book-title" {...register('title')} />
        {errors.title && <p className="text-sm text-destructive">{errors.title.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-author">Autor *</Label>
        <Input id="book-author" {...register('author')} />
        {errors.author && <p className="text-sm text-destructive">{errors.author.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-genre">Género *</Label>
        <select id="book-genre" {...register('genre')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {tempGenreOption && !genres.includes(tempGenreOption) && (
            <option key="_tmp" value={tempGenreOption}>{tempGenreOption}</option>
          )}
          {genres.map(g => <option key={g} value={g}>{g}</option>)}
        </select>
        {errors.genre && <p className="text-sm text-destructive">{errors.genre.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-subgenre">Subgénero</Label>
        <select id="book-subgenre" {...register('subgenre')}
          disabled={!selectedGenre || isLoadingSubgenres || subgenres.length === 0}
          required={subgenres.length > 0}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">
            {selectedGenre
              ? isLoadingSubgenres
                ? 'Cargando subgéneros...'
                : subgenres.length === 0
                  ? 'No hay subgéneros para este género'
                  : 'Selecciona...'
              : 'Selecciona un género primero'}
          </option>
          {subgenres.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        {errors.subgenre && <p className="text-sm text-destructive">{errors.subgenre.message}</p>}
      </div>

      <div className="space-y-1 relative">
        <Label htmlFor="country">País</Label>
        <Input
          id="country"
          type="text"
          placeholder="País"
          {...register('country')}
          list="countries-suggestions" // 🚀 Vincula el input con el datalist de abajo
          className="bg-white text-black dark:bg-zinc-800 dark:text-white w-full"
          autoComplete="off" // Desactiva el autocompletado viejo del navegador
        />
        <datalist id="countries-suggestions">
          {COUNTRIES_LIST.map((country) => (
            <option key={country} value={country} />
          ))}
        </datalist>

        {errors.country && (
          <p className="text-sm text-destructive">{errors.country.message}</p>
        )}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-priority">Prioridad (1–5) *</Label>
        <input id="book-priority" type="number" min={1} max={5}
          {...register('priority', { valueAsNumber: true })}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background"
        />
        {errors.priority && <p className="text-sm text-destructive">{errors.priority.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-energy">Energía mental *</Label>
        <select id="book-energy" {...register('mentalEnergy')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {mentalEnergyLevels.map(m => <option key={m} value={m}>{m}</option>)}
        </select>
        {errors.mentalEnergy && <p className="text-sm text-destructive">{errors.mentalEnergy.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-mood">Ánimo *</Label>
        <select id="book-mood" {...register('recommendedMood')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {moods.map(m => <option key={m} value={m}>{m}</option>)}
        </select>
        {errors.recommendedMood && <p className="text-sm text-destructive">{errors.recommendedMood.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-rotation">Categoría de rotación *</Label>
        <select id="book-rotation" {...register('rotationCategory')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {rotationCategories.map(r => <option key={r} value={r}>{r}</option>)}
        </select>
        {errors.rotationCategory && <p className="text-sm text-destructive">{errors.rotationCategory.message}</p>}
      </div>

      <Button type="submit" className="w-full" disabled={isPending}>
        {isPending ? 'Guardando…' : 'Guardar'}
      </Button>
    </form>
  )
}
