interface Props {
  value: number
  max?: number
  onChange?: (v: number) => void
}

export function StarRating({ value, max = 5, onChange }: Props) {
  return (
    <span className="flex gap-0.5 text-amber-400">
      {Array.from({ length: max }, (_, i) => (
        <span
          key={i}
          className={onChange ? 'cursor-pointer select-none' : 'select-none'}
          onClick={() => onChange?.(i + 1)}
          aria-hidden="true"
        >
          {i < value ? '★' : '☆'}
        </span>
      ))}
    </span>
  )
}
