import { RATING_SCALE } from '../lib/appraisalForms'
import { cx } from './util'

/** 1–5 selector used on every appraisal form. Read-only when onChange is omitted. */
export function Rating1to5({ value, onChange, disabled }: {
  value?: number | null; onChange?: (v: number) => void; disabled?: boolean
}) {
  return (
    <div className="flex gap-1.5" role="radiogroup">
      {[1, 2, 3, 4, 5].map((n) => {
        const active = value === n
        return (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={active}
            aria-label={`${n} — ${RATING_SCALE.find((s) => s.v === n)?.label}`}
            title={RATING_SCALE.find((s) => s.v === n)?.label}
            disabled={disabled || !onChange}
            onClick={() => onChange?.(n)}
            className={cx(
              'h-9 w-9 rounded-xl border text-sm font-bold transition',
              active
                ? 'border-brand-500 bg-brand-500 text-white shadow-sm shadow-brand-500/40'
                : 'border-[var(--line)] text-slate-500 hover:border-brand-300 hover:text-brand-600 dark:hover:bg-navy-600',
              (disabled || !onChange) && 'opacity-50 pointer-events-none',
            )}>
            {n}
          </button>
        )
      })}
    </div>
  )
}

/** The shared legend explaining what each number means. */
export function RatingLegend({ className }: { className?: string }) {
  return (
    <div className={cx('rounded-xl border border-[var(--line)] overflow-hidden', className)}>
      {RATING_SCALE.map((s) => (
        <div key={s.v} className="flex items-start gap-3 px-3.5 py-2 border-b border-[var(--line)] last:border-0 text-sm">
          <span className="grid place-items-center h-6 w-6 shrink-0 rounded-lg bg-brand-50 dark:bg-brand-900/40 text-brand-600 dark:text-brand-300 text-xs font-bold">{s.v}</span>
          <span><span className="font-semibold">{s.label}</span> <span className="text-slate-500">— {s.hint}</span></span>
        </div>
      ))}
    </div>
  )
}
