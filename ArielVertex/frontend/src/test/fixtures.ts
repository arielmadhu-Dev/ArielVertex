import type { CurrentUser } from '../lib/types'

export function user(permissions: string[] = [], overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 1,
    name: 'Test User',
    email: 'test@arielsoftwares.in',
    employeeCode: 'AV0001',
    role: 'Employee',
    roleLabel: 'Employee',
    designation: 'Engineer',
    avatarColor: '#123456',
    dashboard: 'employee',
    permissions,
    ...overrides,
  }
}
