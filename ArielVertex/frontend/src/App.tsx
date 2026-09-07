import { Routes, Route, Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './lib/auth'
import { VertexMark } from './ui/Logo'
import { AppLayout } from './components/AppLayout'
import { ErrorBoundary } from './components/ErrorBoundary'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Projects from './pages/Projects'
import ProjectWorkspace from './pages/ProjectWorkspace'
import StatusUpdates from './pages/StatusUpdates'
import Reviews from './pages/Reviews'
import Feedback from './pages/Feedback'
import MyPerformance from './pages/MyPerformance'
import PerformanceReports from './pages/PerformanceReports'
import PerformanceReportPrint from './pages/PerformanceReportPrint'
import ManagementReportPrint from './pages/ManagementReportPrint'
import Resources from './pages/Resources'
import Hiring from './pages/Hiring'
import Employees from './pages/Employees'
import Expenses from './pages/Expenses'
import Bills from './pages/Bills'
import PipPage from './pages/Pip'
import Meetings from './pages/Meetings'
import Configuration from './pages/Configuration'
import NotificationsPage from './pages/NotificationsPage'
import Admin from './pages/Admin'
import Reports from './pages/Reports'
import Cycles from './pages/Cycles'
import Appraisals from './pages/Appraisals'
import Goals from './pages/Goals'
import Promotions from './pages/Promotions'
import Learning from './pages/Learning'
import Analytics from './pages/Analytics'

function Splash() {
  return (
    <div className="min-h-screen grid place-items-center av-mesh">
      <div className="flex flex-col items-center gap-4 animate-pulse">
        <VertexMark size={54} className="text-navy dark:text-white" />
        <p className="text-sm font-semibold text-slate-400">Loading Ariel Vertex…</p>
      </div>
    </div>
  )
}

function Protected({ children }: { children: JSX.Element }) {
  const { user, loading } = useAuth()
  const loc = useLocation()
  if (loading) return <Splash />
  if (!user) return <Navigate to="/login" state={{ from: loc }} replace />
  return children
}

export default function App() {
  const { user, loading } = useAuth()
  const location = useLocation()

  return (
    <ErrorBoundary resetKey={`${location.pathname}${location.search}`}>
    <Routes>
        <Route path="/login" element={user && !loading ? <Navigate to="/" replace /> : <Login />} />
        <Route path="/report/:id" element={<Protected><PerformanceReportPrint /></Protected>} />
        <Route path="/reports/print" element={<Protected><ManagementReportPrint /></Protected>} />
        <Route element={<Protected><AppLayout /></Protected>}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/projects" element={<Projects />} />
          <Route path="/projects/:id" element={<ProjectWorkspace />} />
          <Route path="/status-updates" element={<StatusUpdates />} />
          <Route path="/reviews" element={<Reviews />} />
          <Route path="/feedback" element={<Feedback />} />
          <Route path="/my-performance" element={<MyPerformance />} />
          <Route path="/performance-reports" element={<PerformanceReports />} />
          <Route path="/resources" element={<Resources />} />
          <Route path="/hiring" element={<Hiring />} />
          <Route path="/employees" element={<Employees />} />
          <Route path="/expenses" element={<Expenses />} />
          <Route path="/bills" element={<Bills />} />
          <Route path="/pip" element={<PipPage />} />
          <Route path="/meetings" element={<Meetings />} />
          <Route path="/configuration" element={<Configuration />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/reports" element={<Reports />} />
          <Route path="/appraisals" element={<Appraisals />} />
          <Route path="/goals" element={<Goals />} />
          <Route path="/cycles" element={<Cycles />} />
          <Route path="/promotions" element={<Promotions />} />
          <Route path="/learning" element={<Learning />} />
          <Route path="/analytics" element={<Analytics />} />
          <Route path="/admin" element={<Admin />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
    </Routes>
    </ErrorBoundary>
  )
}
