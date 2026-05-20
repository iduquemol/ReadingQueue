import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-4xl font-bold">404</h1>
      <p className="text-muted-foreground">Página no encontrada.</p>
      <Link to="/library" className="text-primary underline">
        Volver a la biblioteca
      </Link>
    </main>
  )
}
