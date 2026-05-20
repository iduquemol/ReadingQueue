export interface User {
  id:          number
  email:       string
  displayName: string
}

export interface Book {
  id:               number
  userId:           number
  title:            string
  author:           string
  genre:            string
  country:          string
  whyRead:          string | null
  priority:         number
  mentalEnergy:     string
  recommendedMood:  string
  rotationCategory: string
  isRead:           boolean
  readAt:           string | null
  notes:            string | null
  createdAt:        string
  updatedAt:        string
}

export interface QueueItem {
  position:    number
  addedAt:     string
  source:      'Manual' | 'AI' | 'Filter'
  aiReasoning: string | null
  book:        Book
}

export interface GenerateQueueResponse {
  aiContributed: boolean
  queue:         QueueItem[]
}

export interface AISuggestion {
  bookId:      number
  bookTitle:   string
  score:       number
  reasoning:   string
  generatedAt: string
  wasAccepted: boolean | null
}

export interface GenreStat {
  genre:  string
  total:  number
  read:   number
  unread: number
}

export interface RotationStat {
  category: string
  total:    number
  read:     number
  unread:   number
}

export interface MentalEnergyStat {
  level:  string
  total:  number
  unread: number
}

export interface CountryStat {
  country: string
  total:   number
}

export interface DashboardStats {
  totalBooks:         number
  readBooks:          number
  unreadBooks:        number
  readPercentage:     number
  byGenre:            GenreStat[]
  byRotationCategory: RotationStat[]
  byMentalEnergy:     MentalEnergyStat[]
  byCountry:          CountryStat[]
  topUnreadPriority:  Book[]
  recentlyRead:       Book[]
}

export interface SpecialLists {
  next5:          Book[]
  whenTired:      Book[]
  historicalDebt: Book[]
}

export interface BookFilters {
  genre?:        string
  country?:      string
  mentalEnergy?: string
  mood?:         string
  rotation?:     string
  minPriority?:  number
  isRead?:       boolean
  q?:            string
}

export interface CreateBookPayload {
  title:            string
  author:           string
  genre:            string
  country:          string
  whyRead?:         string
  priority:         number
  mentalEnergy:     string
  recommendedMood:  string
  rotationCategory: string
  notes?:           string
}

export type UpdateBookPayload = CreateBookPayload

export interface MarkAsReadPayload {
  readAt?: string
  notes?:  string
}

export interface LoginPayload {
  email:    string
  password: string
}

export interface RegisterPayload {
  email:       string
  password:    string
  displayName: string
}

export interface AuthResponse {
  accessToken:  string
  refreshToken: string
  userId:       number
  displayName:  string
}
