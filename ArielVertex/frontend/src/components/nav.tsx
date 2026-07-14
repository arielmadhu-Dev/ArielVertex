import {
  LayoutDashboard, FolderKanban, ClipboardCheck, MessageSquareQuote, LineChart,
  UsersRound, UserPlus, GaugeCircle, FileBarChart, ShieldCheck, CalendarClock, Gauge, Wallet,
  LifeBuoy, SlidersHorizontal, NotebookPen, LucideIcon,
} from 'lucide-react'
import type { CurrentUser } from '../lib/types'

// Capability strings — must match backend Permissions catalogue.
export const P = {
  ProjectsViewAll: 'projects.view.all', ProjectsCreate: 'projects.create', StatusSubmit: 'status.submit', StatusViewAll: 'status.view.all',
  ReviewsRequest: 'reviews.request', ReviewsSchedule: 'reviews.schedule', ReviewsSubmit: 'reviews.submit',
  FeedbackSubmit: 'feedback.submit', FeedbackApprove: 'feedback.approve', PerformanceViewOwn: 'performance.view.own',
  PerformanceViewAll: 'performance.view.all', PerformancePublish: 'performance.publish',
  ResourcesRequest: 'resources.request', ResourcesManage: 'resources.manage', ResourcesViewAll: 'resources.view.all',
  ReportsView: 'reports.view', EmployeesManage: 'employees.manage', AuditView: 'audit.view',
  SyncRun: 'sync.run', AdminSettings: 'admin.settings',
  ExpensesManage: 'expenses.manage', ExpensesApprove: 'expenses.approve',
  ExpensesViewAll: 'expenses.view.all', ExpensesConfigure: 'expenses.configure',
  PipView: 'pip.view', PipManage: 'pip.manage', ConfigManage: 'config.manage',
  MinutesManage: 'minutes.manage',
}

export interface NavItem { to: string; label: string; icon: LucideIcon; show: (u: CurrentUser) => boolean; feature?: string }
export interface NavGroup { title: string; items: NavItem[] }

const any = (u: CurrentUser, ...perms: string[]) => perms.some((p) => u.permissions.includes(p))

export const navGroups: NavGroup[] = [
  {
    title: 'Overview',
    items: [
      { to: '/', label: 'Dashboard', icon: LayoutDashboard, show: () => true },
      { to: '/projects', label: 'Projects', icon: FolderKanban, show: () => true },
      { to: '/status-updates', label: 'Status Updates', icon: CalendarClock, show: (u) => any(u, P.StatusSubmit, P.StatusViewAll) },
    ],
  },
  {
    title: 'Review & Growth',
    items: [
      { to: '/reviews', label: 'Reviews', icon: ClipboardCheck, feature: 'reviews', show: (u) => any(u, P.ReviewsRequest, P.ReviewsSchedule, P.ReviewsSubmit) },
      { to: '/meetings', label: 'Meetings & Minutes', icon: NotebookPen, feature: 'minutes', show: (u) => any(u, P.MinutesManage) },
      { to: '/feedback', label: 'Feedback', icon: MessageSquareQuote, feature: 'feedback', show: (u) => any(u, P.FeedbackSubmit, P.FeedbackApprove) },
      { to: '/my-performance', label: 'My Performance', icon: LineChart, show: (u) => any(u, P.PerformanceViewOwn) },
      { to: '/performance-reports', label: 'Performance Reports', icon: Gauge, feature: 'performanceReports', show: (u) => any(u, P.PerformanceViewAll) },
      { to: '/pip', label: 'Improvement Plans', icon: LifeBuoy, feature: 'pip', show: (u) => any(u, P.PipView) },
    ],
  },
  {
    title: 'People & Resources',
    items: [
      { to: '/resources', label: 'Resource Visibility', icon: GaugeCircle, feature: 'resources', show: (u) => any(u, P.ResourcesViewAll) },
      { to: '/hiring', label: 'Hiring Requests', icon: UserPlus, feature: 'hiring', show: (u) => any(u, P.ResourcesRequest, P.ResourcesManage) },
      { to: '/employees', label: 'Employees', icon: UsersRound, show: (u) => any(u, P.EmployeesManage, P.ProjectsViewAll) },
      { to: '/expenses', label: 'Expenses', icon: Wallet, feature: 'expenses', show: (u) => any(u, P.ExpensesManage, P.ExpensesApprove, P.ExpensesViewAll) },
      { to: '/reports', label: 'Reports', icon: FileBarChart, feature: 'reports', show: (u) => any(u, P.ReportsView) },
    ],
  },
  {
    title: 'Administration',
    items: [
      { to: '/admin', label: 'Admin & Audit', icon: ShieldCheck, show: (u) => any(u, P.AuditView, P.SyncRun, P.AdminSettings) },
      { to: '/configuration', label: 'Configuration', icon: SlidersHorizontal, show: (u) => any(u, P.ConfigManage) },
    ],
  },
]

/** Nav filtered by role permissions AND admin feature flags (a module turned off in Config disappears). */
export function visibleGroups(u: CurrentUser, features?: Record<string, boolean>): NavGroup[] {
  return navGroups
    .map((g) => ({ ...g, items: g.items.filter((i) => i.show(u) && (!i.feature || features?.[i.feature] !== false)) }))
    .filter((g) => g.items.length > 0)
}
