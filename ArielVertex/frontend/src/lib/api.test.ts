import { describe, expect, it } from 'vitest'
import { apiError, tokenStore } from './api'

describe('API helpers', () => {
  it('stores and clears the session token', () => {
    tokenStore.set('token-123')
    expect(tokenStore.get()).toBe('token-123')
    tokenStore.clear()
    expect(tokenStore.get()).toBeNull()
  })

  it('prefers the backend error envelope message', () => {
    expect(apiError({ response: { data: { error: { message: 'Access denied' } } } })).toBe('Access denied')
  })

  it('falls back to the native error and then supplied fallback', () => {
    expect(apiError(new Error('Network down'))).toBe('Network down')
    expect(apiError({}, 'Try again')).toBe('Try again')
  })
})
