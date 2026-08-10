import { describe, expect, it } from 'vitest'
import { P, visibleGroups } from './nav'
import { user } from '../test/fixtures'

const labels = (permissions: string[], features?: Record<string, boolean>) =>
  visibleGroups(user(permissions), features).flatMap((group) => group.items.map((item) => item.label))

describe('role-based navigation', () => {
  it('always includes common employee sections', () => {
    const items = labels([])
    expect(items).toContain('Dashboard')
    expect(items).toContain('Projects')
    expect(items).toContain('Improvement Plans')
    expect(items).not.toContain('Admin & Audit')
  })

  it('shows capability-protected sections only when granted', () => {
    const items = labels([P.StatusSubmit, P.PerformanceViewOwn, P.EmployeesManage, P.AuditView])
    expect(items).toContain('Status Updates')
    expect(items).toContain('My Performance')
    expect(items).toContain('Employees')
    expect(items).toContain('Admin & Audit')
  })

  it('hides a module when its feature flag is disabled', () => {
    const items = labels([P.ExpensesManage, P.ReviewsRequest], { expenses: false, reviews: false })
    expect(items).not.toContain('Expenses')
    expect(items).not.toContain('Reviews')
  })

  it('removes groups which have no visible items', () => {
    const groups = visibleGroups(user([]))
    expect(groups.some((g) => g.title === 'Administration')).toBe(false)
  })
})
