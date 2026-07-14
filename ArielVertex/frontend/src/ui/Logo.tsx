import { cx } from './util'

export function VertexMark({ className, size = 32 }: { className?: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" className={className} aria-hidden>
      <path d="M6 12 L26 12 L20 26 L12 26 Z" fill="#1E7FD4" />
      <path d="M20 12 L34 12 L24 52 Z" fill="currentColor" />
    </svg>
  )
}

export function Logo({ compact = false, className }: { compact?: boolean; className?: string }) {
  return (
    <div className={cx('flex items-center gap-2.5 select-none', className)}>
      <VertexMark size={30} className="text-navy dark:text-white shrink-0" />
      {!compact && (
        <div className="leading-none">
          <div className="text-[17px] font-extrabold tracking-tight">
            <span className="text-navy dark:text-white">Ariel</span>{' '}
            <span className="text-brand-500">Vertex</span>
          </div>
          <div className="mt-0.5 text-[9.5px] font-medium uppercase tracking-[0.14em] text-slate-400">
            Operations · Review · Performance
          </div>
        </div>
      )}
    </div>
  )
}
