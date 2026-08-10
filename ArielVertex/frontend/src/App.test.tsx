import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from './App'

vi.mock('./lib/auth', () => ({
  useAuth: () => ({ user: null, loading: false, login: vi.fn(), adopt: vi.fn(), logout: vi.fn(), has: () => false }),
}))
vi.mock('./lib/msal', () => ({ microsoftEnabled: false, signInWithMicrosoft: vi.fn() }))

it('redirects an anonymous user from a protected route to login', async () => {
  render(<MemoryRouter initialEntries={['/projects']} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><App /></MemoryRouter>)
  expect(await screen.findByText('Welcome back')).toBeInTheDocument()
})
