import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { api } from '../lib/api'
import { P } from '../components/nav'
import { renderPage } from '../test/render'
import Reviews from './Reviews'
import PerformanceReports from './PerformanceReports'
import Meetings from './Meetings'
import Admin from './Admin'

const auth = vi.hoisted(() => ({ permissions: [] as string[], id: 1 }))
vi.mock('../lib/auth', () => ({
  useAuth: () => ({ user: { id: auth.id }, has: (permission: string) => auth.permissions.includes(permission) }),
}))

describe('secondary workflow pages', () => {
  beforeEach(() => {
    auth.permissions = []
    vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    vi.spyOn(api, 'get').mockImplementation(async (url: string) => {
      if (url === '/employees') return { data: { items: [] } } as never
      if (url === '/meta/enums') return { data: { reviewTypes: [], projectStatus: [] } } as never
      if (url === '/performance/categories') return { data: { categories: [] } } as never
      if (url === '/admin/integration-status') return { data: { authMode: 'Local', microsoftLoginEnabled: false, graphMeetingsLive: false, directorySyncLive: false, outlookNotificationsLive: false, allowedDomain: 'arielsoftwares.in' } } as never
      if (url === '/admin/audit') return { data: { items: [], total: 0, page: 1, pageSize: 15, totalPages: 0 } } as never
      return { data: [] } as never
    })
  })

  it('renders review request controls according to permissions', async () => {
    auth.permissions = [P.ReviewsRequest]
    renderPage(<Reviews />)
    expect(await screen.findByText('No review requests')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Request review' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Schedule reviews' })).toBeInTheDocument()
  })

  it('renders the empty performance reporting workspace', async () => {
    auth.permissions = [P.PerformanceViewAll, P.PerformancePublish]
    renderPage(<PerformanceReports />)
    expect(await screen.findByText('Performance Reports')).toBeInTheDocument()
    expect(await screen.findByText('No reports yet')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Generate report' })).toBeDisabled()
  })

  it('opens the new meeting-minutes form', async () => {
    renderPage(<Meetings />)
    expect(await screen.findByText('No minutes yet')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'New minutes' }).length).toBeGreaterThan(0)
  })

  it('renders honest disabled integration status in local admin mode', async () => {
    auth.permissions = [P.AuditView, P.SyncRun]
    renderPage(<Admin />)
    expect(await screen.findByText('Admin & Audit')).toBeInTheDocument()
    expect(await screen.findByText('Local')).toBeInTheDocument()
    expect(await screen.findByText('No sync runs yet')).toBeInTheDocument()
    expect(await screen.findByText('No audit entries yet')).toBeInTheDocument()
  })
})
