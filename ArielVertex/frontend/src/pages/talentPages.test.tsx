import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { api } from '../lib/api'
import { renderPage } from '../test/render'
import { P } from '../components/nav'
import Appraisals from './Appraisals'
import Goals from './Goals'
import Promotions from './Promotions'
import Learning from './Learning'
import Analytics from './Analytics'
import Resources from './Resources'
import Employees from './Employees'

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: { id: 1 },
    has: (permission: string) => [P.AppraisalsManage, P.AppraisalsRelease, P.GoalsAssign, P.GoalsViewAll,
      P.PromotionsRecommend, P.PromotionsApprove, P.PromotionsManage, P.LearningManage, P.AnalyticsView,
      P.EmployeesManage].includes(permission),
  }),
}))

describe('talent and people pages', () => {
  beforeEach(() => {
    vi.spyOn(api, 'get').mockImplementation(async (url: string) => {
      if (url === '/employees') return { data: { items: [], total: 0, page: 1, pageSize: 15, totalPages: 0 } } as never
      if (url === '/employees/edit-options') return { data: { departments: [], managers: [] } } as never
      if (url === '/meta/enums') return { data: { goalStatuses: [], promotionStages: [], trainingStatuses: [], employeeStatuses: [], roles: [] } } as never
      if (url === '/analytics') return { data: null } as never
      if (url === '/resources/occupancy') return { data: { buckets: [], rows: [] } } as never
      return { data: [] } as never
    })
  })

  it('renders the empty appraisal state', async () => {
    renderPage(<Appraisals />)
    expect(await screen.findByText('No appraisals')).toBeInTheDocument()
  })

  it('renders goal assignment controls', async () => {
    renderPage(<Goals />)
    expect(await screen.findByText('No goals yet')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Assign goal' })).toBeInTheDocument()
  })

  it('renders promotion recommendation controls', async () => {
    renderPage(<Promotions />)
    expect(await screen.findByText('No promotions')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Recommend' })).toBeInTheDocument()
  })

  it('renders learning management controls', async () => {
    renderPage(<Learning />)
    expect(await screen.findByText('No training yet')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Assign' })).toBeInTheDocument()
  })

  it('renders the no-data analytics state', async () => {
    renderPage(<Analytics />)
    expect(await screen.findByText('No analytics available')).toBeInTheDocument()
  })

  it('renders the no-allocation resources state', async () => {
    renderPage(<Resources />)
    expect(await screen.findByText('No allocation data')).toBeInTheDocument()
  })

  it('renders the empty employee directory', async () => {
    renderPage(<Employees />)
    expect(await screen.findByText('No employees found')).toBeInTheDocument()
  })
})
