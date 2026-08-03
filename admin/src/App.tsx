import { BrowserRouter, Route, Routes } from 'react-router'
import { Layout } from '@/components/Layout'
import { RequireAuth } from '@/components/RequireAuth'
import { AuthProvider } from '@/lib/auth'
import { CompliancePage } from '@/pages/CompliancePage'
import { DashboardPage } from '@/pages/DashboardPage'
import { EventDetailPage } from '@/pages/EventDetailPage'
import { EventsPage } from '@/pages/EventsPage'
import { HoursPage } from '@/pages/HoursPage'
import { InvoicesPage } from '@/pages/InvoicesPage'
import { LoginPage } from '@/pages/LoginPage'
import { NotificationsPage } from '@/pages/NotificationsPage'
import { StaffingPage } from '@/pages/StaffingPage'
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
              <Route path="evenements" element={<EventsPage />} />
              <Route path="evenements/:id" element={<EventDetailPage />} />
              <Route path="postes/:id/staffing" element={<StaffingPage />} />
              <Route path="heures" element={<HoursPage />} />
              <Route path="factures" element={<InvoicesPage />} />
              <Route path="conformite" element={<CompliancePage />} />
              <Route path="envois" element={<NotificationsPage />} />
              <Route path="statut" element={<StatusPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

