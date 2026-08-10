import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { Routes, Route } from 'react-router-dom'
import { api } from '../lib/api'
import { renderPage } from '../test/render'
import PerformanceReportPrint from './PerformanceReportPrint'
import ManagementReportPrint from './ManagementReportPrint'

describe('print-ready reports', () => {
  beforeEach(() => vi.spyOn(api, 'get'))

  it('renders a performance report and PDF action', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { id: 4, subjectName: 'Rahul', periodType: 'Quarterly', period: '2026-Q2', overallScore: 82, ratingLabel: 'Exceeds Expectations', strengths: 'Ownership', improvementAreas: 'Testing', recommendedActions: 'Add coverage', dataSources: 'Status, reviews', isPublished: true, publishedAt: '2026-07-01', categories: [] } } as never)
    renderPage(<Routes><Route path="/report/:id" element={<PerformanceReportPrint />} /></Routes>, '/report/4')
    expect(await screen.findByText('Performance Report')).toBeInTheDocument()
    expect(screen.getAllByText(/Rahul/).length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: 'Save as PDF' })).toBeInTheDocument()
  })

  it('renders the management portfolio report', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) => url === '/projects'
      ? { data: { items: [{ id: 7, code: 'MIB', name: 'MIB Portal', clientName: 'MIB', status: 'Active', health: 'Green', memberCount: 5 }] } } as never
      : { data: { buckets: [] } } as never)
    renderPage(<ManagementReportPrint />)
    expect(await screen.findByText('Management Report')).toBeInTheDocument()
    expect(screen.getByText('MIB Portal')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save as PDF' })).toBeInTheDocument()
  })
})
