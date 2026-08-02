import { BrowserRouter, Route, Routes } from 'react-router'
import { Layout } from './components/Layout'
import { DemoPage } from './pages/DemoPage'
import { HomePage } from './pages/HomePage'
import { StatusPage } from './pages/StatusPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="demo" element={<DemoPage />} />
          <Route path="statut" element={<StatusPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
