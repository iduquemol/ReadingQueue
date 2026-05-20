export const GENRE_COLORS: Record<string, string> = {
  'No ficcion / ensayo':                   'bg-blue-100 text-blue-800',
  'Clasico':                               'bg-amber-100 text-amber-800',
  'Novela contemporanea':                  'bg-purple-100 text-purple-800',
  'Novela latinoamericana':               'bg-green-100 text-green-800',
  'Cuentos':                              'bg-pink-100 text-pink-800',
  'Novela clasica':                        'bg-orange-100 text-orange-800',
  'Poesia':                               'bg-rose-100 text-rose-800',
  'No ficción / ensayo':                  'bg-blue-100 text-blue-800',
  'Clásico':                              'bg-amber-100 text-amber-800',
  'Novela contemporánea':                 'bg-purple-100 text-purple-800',
  'Novela latinoamericana (con tilde)':   'bg-green-100 text-green-800',
  'Cuentos (alt)':                        'bg-pink-100 text-pink-800',
  'Novela clásica':                       'bg-orange-100 text-orange-800',
  'Poesía':                               'bg-rose-100 text-rose-800',
}

export function getGenreColor(genre: string): string {
  return GENRE_COLORS[genre] ?? 'bg-gray-100 text-gray-800'
}
