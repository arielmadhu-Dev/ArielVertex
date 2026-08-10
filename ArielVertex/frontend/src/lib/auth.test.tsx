import { describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AuthProvider, useAuth } from './auth'
import { api, tokenStore } from './api'
import { user } from '../test/fixtures'

function Probe() {
  const auth = useAuth()
  return (
    <div>
      <span data-testid="loading">{String(auth.loading)}</span>
      <span data-testid="email">{auth.user?.email ?? 'anonymous'}</span>
      <span data-testid="allowed">{String(auth.has('allowed'))}</span>
      <button onClick={() => auth.login('test@arielsoftwares.in', 'password')}>login</button>
      <button onClick={auth.logout}>logout</button>
      <button onClick={() => auth.adopt(user(['allowed']))}>adopt</button>
    </div>
  )
}

describe('AuthProvider', () => {
  it('settles as anonymous when no token exists', async () => {
    render(<AuthProvider><Probe /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'))
    expect(screen.getByTestId('email')).toHaveTextContent('anonymous')
  })

  it('restores an existing session from auth/me', async () => {
    const current = user(['allowed'])
    tokenStore.set('existing')
    vi.spyOn(api, 'get').mockResolvedValueOnce({ data: current } as never)
    render(<AuthProvider><Probe /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('email')).toHaveTextContent(current.email))
    expect(api.get).toHaveBeenCalledWith('/auth/me')
    expect(screen.getByTestId('allowed')).toHaveTextContent('true')
  })

  it('clears an invalid stored session', async () => {
    tokenStore.set('expired')
    vi.spyOn(api, 'get').mockRejectedValueOnce(new Error('unauthorized'))
    render(<AuthProvider><Probe /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'))
    expect(tokenStore.get()).toBeNull()
    expect(screen.getByTestId('email')).toHaveTextContent('anonymous')
  })

  it('logs in, persists the returned token, and logs out', async () => {
    const current = user(['allowed'])
    vi.spyOn(api, 'post').mockResolvedValueOnce({ data: { token: 'new-token', user: current } } as never)
    render(<AuthProvider><Probe /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'))
    await userEvent.click(screen.getByRole('button', { name: 'login' }))
    await waitFor(() => expect(screen.getByTestId('email')).toHaveTextContent(current.email))
    expect(tokenStore.get()).toBe('new-token')
    expect(api.post).toHaveBeenCalledWith('/auth/login', { email: 'test@arielsoftwares.in', password: 'password' })

    await userEvent.click(screen.getByRole('button', { name: 'logout' }))
    expect(tokenStore.get()).toBeNull()
    expect(screen.getByTestId('email')).toHaveTextContent('anonymous')
  })

  it('adopts an externally authenticated user', async () => {
    render(<AuthProvider><Probe /></AuthProvider>)
    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'))
    await act(async () => userEvent.click(screen.getByRole('button', { name: 'adopt' })))
    expect(screen.getByTestId('allowed')).toHaveTextContent('true')
  })
})
