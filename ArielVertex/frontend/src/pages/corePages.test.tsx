import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { api } from '../lib/api'
import { P } from '../components/nav'
import { renderPage } from '../test/render'
import Projects from './Projects'
import StatusUpdates from './StatusUpdates'
import Feedback from './Feedback'
import Expenses from './Expenses'

const auth = vi.hoisted(() => ({ permissions: [] as string[] }))
vi.mock('../lib/auth', () => ({
  useAuth: () => ({ has: (permission: string) => auth.permissions.includes(permission) }),
}))

const enums = {
  projectStatus: [{ value: 'Active', label: 'Active' }], priorities: [{ value: 'Medium', label: 'Medium' }],
  updateStatuses: [{ value: 'OnTrack', label: 'On Track' }], expenseCategories: [{ value: 'OfficeSupplies', label: 'Office Supplies' }],
}

describe('core workflow pages', () => {
  beforeEach(() => {
    auth.permissions = []
    vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)
    vi.spyOn(api, 'get').mockImplementation(async (url: string) => {
      if (url === '/meta/enums') return { data: enums } as never
      return { data: {} } as never
    })
  })

  it('renders scoped projects and hides creation from employees', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) => url === '/projects'
      ? { data: { items: [{ id: 7, code: 'MIB', name: 'MIB Portal', status: 'Active', health: 'Green', priority: 'High', clientName: 'MIB Financial', startDate: '2026-01-01', memberCount: 5, tags: [] }], total: 1 } } as never
      : { data: enums } as never)
    renderPage(<Projects />)
    expect(await screen.findByText('MIB Portal')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New Project' })).not.toBeInTheDocument()
  })

  it('allows a project creator to open and submit the project form', async () => {
    auth.permissions = [P.ProjectsCreate]
    vi.mocked(api.get).mockImplementation(async (url: string) => url === '/projects'
      ? { data: { items: [], total: 0 } } as never : { data: enums } as never)
    renderPage(<Projects />)
    await userEvent.click(await screen.findByRole('button', { name: 'New Project' }))
    await userEvent.type(screen.getByPlaceholderText('MIB'), 'abc')
    await userEvent.type(screen.getByPlaceholderText('Project name'), 'Alpha Project')
    await userEvent.click(screen.getByRole('button', { name: 'Create project' }))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/projects', expect.objectContaining({ code: 'ABC', name: 'Alpha Project' })))
  })

  it('enables status submission only after required fields are provided', async () => {
    auth.permissions = [P.StatusSubmit]
    vi.mocked(api.get).mockImplementation(async (url: string) => {
      if (url === '/status-updates/mine') return { data: { updates: [], assignableProjects: [{ projectId: 7, name: 'MIB Portal' }] } } as never
      return { data: enums } as never
    })
    renderPage(<StatusUpdates />)
    const submit = await screen.findByRole('button', { name: 'Submit update' })
    expect(submit).toBeDisabled()
    await screen.findByRole('option', { name: 'MIB Portal' })
    await userEvent.selectOptions(screen.getAllByRole('combobox')[0], '7')
    await userEvent.type(screen.getByPlaceholderText('What did you finish today?'), 'Completed API tests')
    expect(submit).toBeEnabled()
    await userEvent.click(submit)
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/status-updates', expect.objectContaining({ projectId: 7, workCompleted: 'Completed API tests' })))
  })

  it('shows feedback approval actions only to approvers', async () => {
    const item = { id: 3, subjectName: 'Rahul', avatarColor: '#123', authorName: 'Shepherd', period: '2026-Q3', status: 'Submitted', constructiveSummary: 'Good progress', createdAt: '2026-08-01', categoryScores: [] }
    vi.mocked(api.get).mockImplementation(async (url: string) => url === '/employees'
      ? { data: { items: [] } } as never
      : { data: [item] } as never)
    const view = renderPage(<Feedback />)
    expect(await screen.findByText('Good progress')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    view.unmount()

    auth.permissions = [P.FeedbackApprove]
    renderPage(<Feedback />)
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/feedback/3/approve', {}))
  })

  it('renders expenses and exposes approval actions returned by the API', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) => url === '/expenses'
      ? { data: { items: [{ id: 5, title: 'Office stationery', categoryLabel: 'Office Supplies', amount: 1200, status: 'PaymentRequested', vendor: 'Vendor', expenseDate: '2026-08-01', raisedByName: 'Front Desk', approvalRequired: true, hasInvoiceFile: false, canApprove: true, canManage: false }], summary: { total: 1, pendingApproval: 1, paid: 0, totalAmount: 1200, paidAmount: 0 } } } as never
      : { data: enums } as never)
    renderPage(<Expenses />)
    expect(await screen.findByText('Office stationery')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/expenses/5/approve', { note: 'Approved' }))
  })
})
