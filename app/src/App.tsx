import { BrowserRouter, Route, Routes } from 'react-router'
import { Layout } from '@/components/Layout'
import { RequireAuth } from '@/components/RequireAuth'
import { AuthProvider } from '@/lib/auth'
import { AvailabilitiesPage } from '@/pages/AvailabilitiesPage'
import { DocumentsPage } from '@/pages/DocumentsPage'
import { HomePage } from '@/pages/HomePage'
import { HoursPage } from '@/pages/HoursPage'
import { LoginPage } from '@/pages/LoginPage'
import { MissionsPage } from '@/pages/MissionsPage'
import { ProfilePage } from '@/pages/ProfilePage'
import { StatusPage } from '@/pages/StatusPage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/connexion" element={<LoginPage />} />

          <Route element={<RequireAuth />}>
            <Route element={<Layout />}>
              <Route index element={<HomePage />} />
              <Route path="missions" element={<MissionsPage />} />
              <Route path="heures" element={<HoursPage />} />
              <Route path="dispos" element={<AvailabilitiesPage />} />
              <Route path="profil" element={<ProfilePage />} />
              <Route path="documents" element={<DocumentsPage />} />
              <Route path="statut" element={<StatusPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

