import { BrowserRouter, Route, Routes } from 'react-router'
import { Layout } from '@/components/Layout'
import { RequireAuth } from '@/components/RequireAuth'
import { AuthProvider } from '@/lib/auth'
import { CompliancePage } from '@/pages/CompliancePage'
import { DashboardPage } from '@/pages/DashboardPage'
import { LoginPage } from '@/pages/LoginPage'
import { StatusPage } from '@/pages/StatusPage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/connexion" element={<LoginPage />} />

          <Route element={<RequireAuth />}>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              <Route path="conformite" element={<CompliancePage />} />
              <Route path="statut" element={<StatusPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
