import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Routes, Route } from 'react-router-dom'
import { api } from '../lib/api'
import { renderPage } from '../test/render'
import ProjectWorkspace from './ProjectWorkspace'
import NotificationsPage from './NotificationsPage'
import Configuration from './Configuration'

vi.mock('../lib/auth', () => ({
  useAuth: () => ({ user: { id: 15 }, has: () => true }),
}))

const project = {
  id: 7, code: 'MIB', name: 'MIB Portal', description: 'Brokerage portal', status: 'Active', health: 'Green', priority: 'High',
  startDate: '2026-01-01', expectedEndDate: '2026-12-31', clientName: 'MIB Financial', businessOwner: 'Arveen',
  tags: ['finance'], notes: 'Weekly delivery', canManage: true, myRoleOnProject: 'ProjectManager',
  members: [{ id: 1, userId: 15, name: 'Rahul', email: 'rahul@arielsoftwares.in', designation: 'Engineer', avatarColor: '#123', roleOnProject: 'Developer', allocationPct: 100, isActive: true, startDate: '2026-01-01' }],
}

describe('remaining workflow pages', () => {
  beforeEach(() => {
    vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    vi.spyOn(api, 'put').mockResolvedValue({ data: {} } as never)
    vi.spyOn(api, 'get').mockImplementation(async (url: string) => {
      if (url === '/projects/7') return { data: project } as never
      if (url === '/employees') return { data: { items: [] } } as never
      if (url === '/meta/enums') return { data: { projectRoles: [], documentCategories: [], visibilities: [], callTypes: [] } } as never
      return { data: [] } as never
    })
  })

  it('renders project details and navigates workspace tabs', async () => {
    renderPage(<Routes><Route path="/projects/:id" element={<ProjectWorkspace />} /></Routes>, '/projects/7')
    expect(await screen.findByText('MIB Portal')).toBeInTheDocument()
    expect(screen.getByText('Brokerage portal')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Team/ }))
    expect(await screen.findByText('Rahul')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add member' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Documents' }))
    expect(await screen.findByText('No documents yet')).toBeInTheDocument()
  })

  it('marks notifications read and follows their link', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { unread: 1, items: [{ id: 9, title: 'Review scheduled', message: 'Open the review', link: '/reviews', isRead: false, createdAt: new Date().toISOString() }] } } as never)
    renderPage(<Routes><Route path="/notifications" element={<NotificationsPage />} /><Route path="/reviews" element={<h1>Reviews destination</h1>} /></Routes>, '/notifications')
    expect(await screen.findByText('Review scheduled')).toBeInTheDocument()
    await userEvent.click(screen.getByText('Review scheduled'))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/notifications/9/read'))
    expect(await screen.findByText('Reviews destination')).toBeInTheDocument()
  })

  it('marks every notification as read', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { unread: 2, items: [] } } as never)
    renderPage(<NotificationsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Mark all read' }))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/notifications/read-all'))
  })

  it('edits and saves a configuration value', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) => url === '/admin/config'
      ? { data: [{ key: 'feature.reviews', value: 'true', group: 'Modules', label: 'Reviews', type: 'bool', description: 'Review module', editable: true }] } as never
      : { data: [] } as never)
    renderPage(<Configuration />)
    expect(await screen.findByText('Reviews')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: '' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Save 1 change(s)' }))
    await waitFor(() => expect(api.put).toHaveBeenCalledWith('/admin/config', { settings: [{ key: 'feature.reviews', value: 'false' }] }))
  })
})
