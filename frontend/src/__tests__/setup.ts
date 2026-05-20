import '@testing-library/jest-dom'

// jsdom no implementa window.matchMedia — shadcn y algunos hooks lo necesitan
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches:             false,
    media:               query,
    onchange:            null,
    addListener:         vi.fn(),
    removeListener:      vi.fn(),
    addEventListener:    vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent:       vi.fn(),
  })),
})

// jsdom no implementa ResizeObserver
global.ResizeObserver = vi.fn().mockImplementation(() => ({
  observe:   vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}))
