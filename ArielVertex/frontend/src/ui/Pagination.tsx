import { ButtonHTMLAttributes, ReactNode } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cx } from './util'

interface PaginationProps {
  page: number
  pageSize: number
  total: number
  totalPages: number
  onPageChange: (page: number) => void
  onPageSizeChange?: (size: number) => void
  pageSizeOptions?: number[]
  /** Dim + disable while a new page is being fetched. */
  busy?: boolean
  className?: string
}

/** Windowed page list: first, last, current ±1, with … gaps. */
function pageList(current: number, totalPages: number): (number | 'gap')[] {
  const out: (number | 'gap')[] = []
  for (let p = 1; p <= totalPages; p++) {
    if (p === 1 || p === totalPages || (p >= current - 1 && p <= current + 1)) out.push(p)
    else if (out[out.length - 1] !== 'gap') out.push('gap')
  }
  return out
}

function PagerBtn({ children, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { children: ReactNode }) {
  return (
    <button
      {...rest}
      className="grid place-items-center h-8 w-8 rounded-lg border border-[var(--line)] text-slate-500 transition hover:bg-slate-100 dark:hover:bg-navy-600 disabled:opacity-40 disabled:pointer-events-none">
      {children}
    </button>
  )
}

/**
 * Server-side pagination control. Pass the values echoed back by the API's
 * PagedResult (page/pageSize/total/totalPages) — the component only navigates,
 * the server does the actual paging.
 */
export function Pagination({
  page, pageSize, total, totalPages,
  onPageChange, onPageSizeChange, pageSizeOptions = [10, 15, 25, 50], busy, className,
}: PaginationProps) {
  if (total === 0) return null
  const from = (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)

  return (
    <div className={cx('flex flex-wrap items-center justify-between gap-3 px-1 py-3', busy && 'opacity-70', className)}>
      <p className="text-xs text-slate-500">
        Showing <span className="font-semibold text-slate-700 dark:text-slate-200">{from.toLocaleString()}–{to.toLocaleString()}</span>
        {' '}of <span className="font-semibold text-slate-700 dark:text-slate-200">{total.toLocaleString()}</span>
      </p>

      <div className="flex items-center gap-2">
        {onPageSizeChange && (
          <label className="hidden sm:flex items-center gap-1.5 text-xs text-slate-500">
            Rows
            <select
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              className="rounded-lg border border-[var(--line)] bg-[var(--card)] px-2 py-1 text-xs font-semibold">
              {pageSizeOptions.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
        )}

        {totalPages > 1 && (
          <div className="flex items-center gap-1">
            <PagerBtn disabled={busy || page <= 1} onClick={() => onPageChange(page - 1)} aria-label="Previous page">
              <ChevronLeft className="h-4 w-4" />
            </PagerBtn>
            {pageList(page, totalPages).map((p, i) => p === 'gap'
              ? <span key={`gap-${i}`} className="px-1.5 text-sm text-slate-400 select-none">…</span>
              : (
                <button
                  key={p}
                  disabled={busy}
                  onClick={() => onPageChange(p)}
                  aria-current={p === page ? 'page' : undefined}
                  className={cx('min-w-[32px] h-8 px-1 rounded-lg text-sm font-semibold transition disabled:pointer-events-none',
                    p === page
                      ? 'bg-brand-500 text-white shadow-sm shadow-brand-500/30'
                      : 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-navy-600')}>
                  {p}
                </button>
              ))}
            <PagerBtn disabled={busy || page >= totalPages} onClick={() => onPageChange(page + 1)} aria-label="Next page">
              <ChevronRight className="h-4 w-4" />
            </PagerBtn>
          </div>
        )}
      </div>
    </div>
  )
}
