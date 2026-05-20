import { create } from 'zustand'

interface UIState {
  sidebarOpen:     boolean
  bookModalOpen:   boolean
  bookModalBookId: number | null
  readModalBookId: number | null
  deleteBookId:    number | null

  openCreateModal:  () => void
  openEditModal:    (id: number) => void
  openReadModal:    (id: number) => void
  openDeleteDialog: (id: number) => void
  closeAll:         () => void
  toggleSidebar:    () => void
}

export const useUIStore = create<UIState>((set) => ({
  sidebarOpen:     false,
  bookModalOpen:   false,
  bookModalBookId: null,
  readModalBookId: null,
  deleteBookId:    null,

  openCreateModal:  () => set({ bookModalOpen: true, bookModalBookId: null }),
  openEditModal:    (id) => set({ bookModalOpen: true, bookModalBookId: id }),
  openReadModal:    (id) => set({ readModalBookId: id }),
  openDeleteDialog: (id) => set({ deleteBookId: id }),
  closeAll: () => set({
    bookModalOpen:   false,
    bookModalBookId: null,
    readModalBookId: null,
    deleteBookId:    null,
  }),
  toggleSidebar: () => set(s => ({ sidebarOpen: !s.sidebarOpen })),
}))
