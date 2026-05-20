import { useState } from 'react'
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'

interface Props {
  source:    'Manual' | 'AI' | 'Filter'
  reasoning: string | null
}

export function SuggestionBadge({ source, reasoning }: Props) {
  const [open, setOpen] = useState(false)

  if (source === 'Filter') {
    return (
      <p className="flex items-center gap-1 text-xs text-muted-foreground">
        <span>⚙️</span>
        <span>Generado por algoritmo</span>
      </p>
    )
  }

  if (source !== 'AI' || !reasoning) return null

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <CollapsibleTrigger
        className="flex items-center gap-1 text-xs text-purple-600 hover:text-purple-800"
        aria-label="Ver razonamiento de IA"
      >
        <span>✨</span>
        <span>{open ? 'Ocultar razonamiento' : 'Ver razonamiento'}</span>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <p className="mt-1 rounded-md bg-purple-50 p-2 text-xs text-purple-700">
          {reasoning}
        </p>
      </CollapsibleContent>
    </Collapsible>
  )
}
