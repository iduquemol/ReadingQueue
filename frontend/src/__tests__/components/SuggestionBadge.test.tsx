import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SuggestionBadge } from '@/components/queue/SuggestionBadge'

const REASONING = 'Este libro encaja perfectamente con tu ánimo actual.'

describe('SuggestionBadge — source=AI', () => {
  it('muestra ✨ y el razonamiento es accesible cuando reasoning no es null (CA-15)', () => {
    render(<SuggestionBadge source="AI" reasoning={REASONING} />)
    expect(screen.getByText(/✨/)).toBeInTheDocument()
  })

  it('no renderiza nada cuando source=AI y reasoning=null', () => {
    const { container } = render(<SuggestionBadge source="AI" reasoning={null} />)
    expect(container).toBeEmptyDOMElement()
  })
})

describe('SuggestionBadge — source=Filter', () => {
  it('muestra "Generado por algoritmo" con ícono ⚙️', () => {
    render(<SuggestionBadge source="Filter" reasoning={null} />)
    expect(screen.getByText(/generado por algoritmo/i)).toBeInTheDocument()
    expect(screen.getByText(/⚙️/)).toBeInTheDocument()
  })
})

describe('SuggestionBadge — colapsable', () => {
  it('click en el trigger muestra el texto; segundo click lo oculta', async () => {
    const user = userEvent.setup()
    render(<SuggestionBadge source="AI" reasoning={REASONING} />)

    expect(screen.queryByText(REASONING)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /razonamiento/i }))
    expect(screen.getByText(REASONING)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /razonamiento/i }))
    expect(screen.queryByText(REASONING)).not.toBeInTheDocument()
  })
})
