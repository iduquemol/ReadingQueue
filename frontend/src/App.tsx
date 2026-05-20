import { Routes, Route, Navigate } from 'react-router-dom'
import { ProtectedRoute }  from '@/components/layout/ProtectedRoute'
import { AppShell }        from '@/components/layout/AppShell'
import { LoginPage }       from '@/pages/LoginPage'
import { RegisterPage }    from '@/pages/RegisterPage'
import { LibraryPage }     from '@/pages/LibraryPage'
import { QueuePage }       from '@/pages/QueuePage'
import { StatsPage }       from '@/pages/StatsPage'
import { NotFoundPage }    from '@/pages/NotFoundPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login"    element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/library" replace />} />
          <Route path="/library"          element={<LibraryPage />} />
          <Route path="/library/new"      element={<LibraryPage />} />
          <Route path="/library/:id/edit" element={<LibraryPage />} />
          <Route path="/queue"            element={<QueuePage />} />
          <Route path="/stats"            element={<StatsPage />} />
        </Route>
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
