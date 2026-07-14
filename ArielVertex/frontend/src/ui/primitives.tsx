import { ButtonHTMLAttributes, HTMLAttributes, ReactNode, forwardRef } from 'react'
import { motion } from 'framer-motion'
import { Loader2 } from 'lucide-react'
import { asText, cx, initials, nice, tone, Tone, statusTone } from './util'

// ---------------- Button ----------------
type Variant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'subtle'
interface BtnProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant; loading?: boolean; icon?: ReactNode; size?: 'sm' | 'md'
}
const variants: Record<Variant, string> = {
  primary: 'bg-brand-500 text-white hover:bg-brand-600 shadow-sm shadow-brand-500/30',
  secondary: 'bg-white dark:bg-navy-600 text-navy dark:text-white border border-[var(--line)] hover:bg-slate-50 dark:hover:bg-navy-500',
  ghost: 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-navy-600',
  danger: 'bg-rose-500 text-white hover:bg-rose-600',
  subtle: 'bg-brand-50 text-brand-700 hover:bg-brand-100 dark:bg-brand-900/40 dark:text-brand-200',
}
export const Button = forwardRef<HTMLButtonElement, BtnProps>(function Button(
  { variant = 'primary', loading, icon, size = 'md', className, children, disabled, ...rest }, ref) {
  return (
    <button ref={ref} disabled={disabled || loading}
      className={cx('inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition active:scale-[.98] disabled:opacity-50 disabled:pointer-events-none',
        size === 'sm' ? 'px-3 py-1.5 text-xs' : 'px-4 py-2.5 text-sm', variants[variant], className)}
      {...rest}>
      {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : icon}
      {children}
    </button>
  )
})

// ---------------- Card ----------------
export function Card({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('av-card', className)} {...rest}>{children}</div>
}
export function CardHeader({ title, subtitle, action, icon }: { title: ReactNode; subtitle?: ReactNode; action?: ReactNode; icon?: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3 px-5 pt-4 pb-3 border-b border-[var(--line)]">
      <div className="flex items-center gap-2.5 min-w-0">
        {icon && <div className="text-brand-500">{icon}</div>}
        <div className="min-w-0">
          <h3 className="font-bold text-[15px] truncate">{title}</h3>
          {subtitle && <p className="text-xs text-slate-500 mt-0.5">{subtitle}</p>}
        </div>
      </div>
      {action}
    </div>
  )
}

// ---------------- Badge / StatusPill ----------------
export function Badge({ children, t = 'info', className }: { children: ReactNode; t?: Tone; className?: string }) {
  const c = tone[t]
  return <span className={cx('inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ring-1', c.bg, c.text, c.ring, className)}>{children}</span>
}
export function StatusPill({ value }: { value?: string | null }) {
  const t = statusTone(value)
  const label = nice(value)
  return <Badge t={t}><span className={cx('h-1.5 w-1.5 rounded-full', tone[t].dot)} />{label}</Badge>
}

// ---------------- Avatar ----------------
export function Avatar({ name, color, size = 36 }: { name?: string | null; color?: string; size?: number }) {
  return (
    <div className="grid place-items-center rounded-full font-bold text-white shrink-0 ring-2 ring-white/70 dark:ring-navy-700"
      style={{ width: size, height: size, background: color || '#1E7FD4', fontSize: size * 0.38 }}>
      {initials(asText(name, 'User'))}
    </div>
  )
}

// ---------------- Empty state ----------------
export function EmptyState({ icon, title, hint, action }: { icon?: ReactNode; title: string; hint?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center text-center py-14 px-6">
      <div className="grid place-items-center h-14 w-14 rounded-2xl bg-brand-50 dark:bg-brand-900/40 text-brand-500 mb-4">{icon}</div>
      <p className="font-semibold">{title}</p>
      {hint && <p className="text-sm text-slate-500 mt-1 max-w-sm">{hint}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

// ---------------- Skeleton ----------------
export function Skeleton({ className }: { className?: string }) {
  return <div className={cx('relative overflow-hidden rounded-lg bg-slate-200/70 dark:bg-navy-600', className)}>
    <div className="absolute inset-0 -translate-x-full animate-shimmer bg-gradient-to-r from-transparent via-white/40 dark:via-white/5 to-transparent" />
  </div>
}

// ---------------- Section transition wrapper ----------------
export function Fade({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.28, ease: 'easeOut' }} className={className}>
      {children}
    </motion.div>
  )
}
