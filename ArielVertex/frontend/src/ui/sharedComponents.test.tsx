import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Rating1to5 } from './RatingScale'
import { Pagination } from './Pagination'
import { Modal } from './Modal'

describe('shared UI components', () => {
  it('selects a rating and exposes the active value accessibly', async () => {
    const change = vi.fn()
    render(<Rating1to5 value={3} onChange={change} />)
    expect(screen.getByRole('radio', { name: /3/ })).toHaveAttribute('aria-checked', 'true')
    await userEvent.click(screen.getByRole('radio', { name: /5/ }))
    expect(change).toHaveBeenCalledWith(5)
  })

  it('makes a read-only rating scale non-interactive', () => {
    render(<Rating1to5 value={2} />)
    expect(screen.getAllByRole('radio')).toHaveLength(5)
    expect(screen.getByRole('radio', { name: /2/ })).toBeDisabled()
  })

  it('navigates pages and reports the visible row range', async () => {
    const page = vi.fn()
    render(<Pagination page={2} pageSize={10} total={34} totalPages={4} onPageChange={page} />)
    expect(screen.getByText(/Showing/).textContent).toContain('11')
    expect(screen.getByText(/Showing/).textContent).toContain('20')
    await userEvent.click(screen.getByRole('button', { name: 'Next page' }))
    expect(page).toHaveBeenCalledWith(3)
  })

  it('does not render pagination for an empty result', () => {
    const { container } = render(<Pagination page={1} pageSize={10} total={0} totalPages={0} onPageChange={() => {}} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('closes a modal with Escape', () => {
    const close = vi.fn()
    render(<Modal open onClose={close} title="Edit item"><p>Body</p></Modal>)
    expect(screen.getByText('Edit item')).toBeInTheDocument()
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(close).toHaveBeenCalledOnce()
  })
})
