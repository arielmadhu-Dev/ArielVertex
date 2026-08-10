import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ErrorBoundary } from './ErrorBoundary'

function Crash(): never { throw new Error('render failed') }

describe('ErrorBoundary', () => {
  it('replaces a crashed page with a recoverable fallback', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    render(<ErrorBoundary><Crash /></ErrorBoundary>)
    expect(screen.getByText('This page hit a snag')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Retry/ })).toBeInTheDocument()
  })
})
