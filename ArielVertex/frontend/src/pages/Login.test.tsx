import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import Login from './Login'
import { user } from '../test/fixtures'

const auth = vi.hoisted(() => ({ login: vi.fn(), adopt: vi.fn() }))
vi.mock('../lib/auth', () => ({ useAuth: () => auth }))
vi.mock('../lib/msal', () => ({ microsoftEnabled: false, signInWithMicrosoft: vi.fn() }))

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/" element={<h1>Dashboard reached</h1>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Login page', () => {
  beforeEach(() => {
    auth.login.mockReset()
    auth.adopt.mockReset()
  })

  it('submits credentials and navigates after successful login', async () => {
    auth.login.mockResolvedValue(user())
    renderLogin()
    const email = screen.getByPlaceholderText('you@arielsoftwares.in')
    await userEvent.clear(email)
    await userEvent.type(email, 'rahul@arielsoftwares.in')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(auth.login).toHaveBeenCalledWith('rahul@arielsoftwares.in', 'Ariel@123'))
    expect(await screen.findByText('Dashboard reached')).toBeInTheDocument()
  })

  it('shows the backend error and stays on the login page', async () => {
    auth.login.mockRejectedValue({ response: { data: { error: { message: 'Invalid email or password.' } } } })
    renderLogin()
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('Invalid email or password.')).toBeInTheDocument()
    expect(screen.getByText('Welcome back')).toBeInTheDocument()
  })

  it('fills a selected demo account', async () => {
    renderLogin()
    await userEvent.click(screen.getByRole('button', { name: /Rahul/ }))
    expect(screen.getByPlaceholderText('you@arielsoftwares.in')).toHaveValue('rahul@arielsoftwares.in')
  })
})
