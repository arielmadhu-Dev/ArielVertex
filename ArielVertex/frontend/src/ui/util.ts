import clsx, { ClassValue } from 'clsx'

export const cx = (...a: ClassValue[]) => clsx(a)

export const initials = (name: string) =>
  name.split(' ').filter(Boolean).slice(0, 2).map((n) => n[0]?.toUpperCase()).join('')

export const fmtDate = (d?: string | null, opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }) =>
  d ? new Date(d).toLocaleDateString(undefined, opts) : '—'

export const fmtDateTime = (d?: string | null) =>
  d ? new Date(d).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : '—'

export const relTime = (d: string) => {
  const diff = (Date.now() - new Date(d).getTime()) / 1000
  const units: [number, string][] = [[60, 'sec'], [3600, 'min'], [86400, 'hr'], [604800, 'day'], [2629800, 'wk']]
  if (diff < 60) return 'just now'
  for (let i = units.length - 1; i >= 0; i--) {
    const [sec, label] = units[i]
    if (diff >= sec) { const v = Math.floor(diff / sec); return `${v} ${label}${v > 1 ? 's' : ''} ago` }
  }
  return 'just now'
}

/** Tone → tailwind class sets used by badges, stat tiles, pills. */
export const tone = {
  brand: { bg: 'bg-brand-50 dark:bg-brand-900/40', text: 'text-brand-700 dark:text-brand-200', ring: 'ring-brand-500/20', dot: 'bg-brand-500' },
  good: { bg: 'bg-emerald-50 dark:bg-emerald-900/30', text: 'text-emerald-700 dark:text-emerald-300', ring: 'ring-emerald-500/20', dot: 'bg-emerald-500' },
  warn: { bg: 'bg-amber-50 dark:bg-amber-900/30', text: 'text-amber-700 dark:text-amber-300', ring: 'ring-amber-500/20', dot: 'bg-amber-500' },
  danger: { bg: 'bg-rose-50 dark:bg-rose-900/30', text: 'text-rose-700 dark:text-rose-300', ring: 'ring-rose-500/20', dot: 'bg-rose-500' },
  info: { bg: 'bg-slate-100 dark:bg-slate-800/60', text: 'text-slate-600 dark:text-slate-300', ring: 'ring-slate-500/10', dot: 'bg-slate-400' },
  neutral: { bg: 'bg-slate-100 dark:bg-slate-800/60', text: 'text-slate-600 dark:text-slate-300', ring: 'ring-slate-500/10', dot: 'bg-slate-400' },
} as const
export type Tone = keyof typeof tone

export const statusTone = (s: string): Tone => {
  const k = s.toLowerCase()
  if (/(green|active|published|approved|completed|fulfilled|ontrack|healthy|outstanding|exceeds)/.test(k)) return 'good'
  if (/(amber|atrisk|underreview|scheduled|inprogress|needsimprovement|pending|open|draft|submitted)/.test(k)) return 'warn'
  if (/(red|blocked|cancelled|rejected|attention|failed|overloaded)/.test(k)) return 'danger'
  return 'info'
}
