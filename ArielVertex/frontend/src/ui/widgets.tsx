import { ReactNode, useState } from 'react'
import { motion } from 'framer-motion'
import {
  Folder, Users, ClipboardList, AlertTriangle, CheckCircle2, UserPlus, Calendar,
  Heart, Edit3, TrendingUp, Star, LucideIcon,
} from 'lucide-react'
import { cx, tone, Tone } from './util'

const iconMap: Record<string, LucideIcon> = {
  folder: Folder, users: Users, clipboard: ClipboardList, alert: AlertTriangle, check: CheckCircle2,
  userplus: UserPlus, calendar: Calendar, heart: Heart, edit: Edit3, trending: TrendingUp,
}

export function StatCard({ label, value, delta, t = 'brand', icon, index = 0 }: {
  label: string; value: string; delta?: string; t?: Tone; icon?: string; index?: number
}) {
  const Icon = icon ? iconMap[icon] ?? Folder : Folder
  const c = tone[t]
  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, delay: index * 0.05 }}
      className="av-card p-4 relative overflow-hidden group">
      <div className={cx('absolute -right-6 -top-6 h-20 w-20 rounded-full opacity-60 blur-2xl transition group-hover:opacity-90', c.bg)} />
      <div className="relative flex items-start justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</p>
          <p className="mt-2 text-2xl font-extrabold tracking-tight">{value}</p>
          {delta && <p className={cx('mt-1 text-xs font-semibold', c.text)}>{delta}</p>}
        </div>
        <div className={cx('grid place-items-center h-10 w-10 rounded-xl', c.bg, c.text)}><Icon className="h-5 w-5" /></div>
      </div>
    </motion.div>
  )
}

export function Tabs({ tabs, active, onChange }: { tabs: { key: string; label: string; badge?: number }[]; active: string; onChange: (k: string) => void }) {
  return (
    <div className="flex gap-1 overflow-x-auto av-scroll-x border-b border-[var(--line)]">
      {tabs.map((t) => {
        const on = t.key === active
        return (
          <button key={t.key} onClick={() => onChange(t.key)}
            className={cx('relative px-3.5 py-2.5 text-sm font-semibold whitespace-nowrap transition',
              on ? 'text-brand-600 dark:text-brand-300' : 'text-slate-500 hover:text-slate-700 dark:hover:text-slate-300')}>
            <span className="inline-flex items-center gap-1.5">{t.label}
              {t.badge ? <span className="grid place-items-center min-w-[18px] h-[18px] px-1 rounded-full bg-brand-100 dark:bg-brand-900/60 text-brand-700 dark:text-brand-200 text-[10px] font-bold">{t.badge}</span> : null}
            </span>
            {on && <motion.div layoutId="tab-underline" className="absolute left-2 right-2 -bottom-px h-0.5 bg-brand-500 rounded-full" />}
          </button>
        )
      })}
    </div>
  )
}

/** Read-only or interactive 1-5 star rating. */
export function Stars({ value, onChange, size = 18 }: { value: number; onChange?: (v: number) => void; size?: number }) {
  const [hover, setHover] = useState(0)
  const active = hover || value
  return (
    <div className="inline-flex gap-0.5">
      {[1, 2, 3, 4, 5].map((n) => (
        <button key={n} type="button" disabled={!onChange}
          onMouseEnter={() => onChange && setHover(n)} onMouseLeave={() => setHover(0)}
          onClick={() => onChange?.(n)} className={cx(onChange ? 'cursor-pointer' : 'cursor-default')}>
          <Star width={size} height={size}
            className={cx('transition', n <= active ? 'fill-amber-400 text-amber-400' : 'text-slate-300 dark:text-navy-500')} />
        </button>
      ))}
    </div>
  )
}

/** Circular score gauge 0-100. */
export function ScoreRing({ score, size = 132 }: { score: number; size?: number }) {
  const r = size / 2 - 10
  const c = 2 * Math.PI * r
  const pct = Math.max(0, Math.min(100, score)) / 100
  const color = score >= 80 ? '#10b981' : score >= 70 ? '#1E7FD4' : score >= 60 ? '#f59e0b' : '#f43f5e'
  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={r} strokeWidth={10} className="stroke-slate-200 dark:stroke-navy-600" fill="none" />
        <motion.circle cx={size / 2} cy={size / 2} r={r} strokeWidth={10} stroke={color} fill="none" strokeLinecap="round"
          strokeDasharray={c} initial={{ strokeDashoffset: c }} animate={{ strokeDashoffset: c - c * pct }}
          transition={{ duration: 1, ease: 'easeOut' }} />
      </svg>
      <div className="absolute inset-0 grid place-items-center">
        <div className="text-center">
          <div className="text-3xl font-extrabold tracking-tight">{score.toFixed(0)}</div>
          <div className="text-[10px] uppercase tracking-wide text-slate-400">out of 100</div>
        </div>
      </div>
    </div>
  )
}

export function Meter({ value, tone: t = 'brand' }: { value: number; tone?: Tone }) {
  const colors: Record<string, string> = { brand: 'bg-brand-500', good: 'bg-emerald-500', warn: 'bg-amber-500', danger: 'bg-rose-500', info: 'bg-slate-400', neutral: 'bg-slate-400' }
  return (
    <div className="h-2 w-full rounded-full bg-slate-200 dark:bg-navy-600 overflow-hidden">
      <motion.div className={cx('h-full rounded-full', colors[t])} initial={{ width: 0 }} animate={{ width: `${Math.min(100, value)}%` }} transition={{ duration: 0.6 }} />
    </div>
  )
}

export function Row({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx('grid gap-4', className)}>{children}</div>
}
