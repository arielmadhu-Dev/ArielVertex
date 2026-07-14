import { ReactNode } from 'react'
import { motion } from 'framer-motion'

export function PageHeader({ title, subtitle, actions, icon }: { title: string; subtitle?: string; actions?: ReactNode; icon?: ReactNode }) {
  return (
    <motion.div initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} className="flex flex-wrap items-end justify-between gap-3 mb-6">
      <div className="flex items-center gap-3">
        {icon && <div className="grid place-items-center h-11 w-11 rounded-2xl bg-brand-50 dark:bg-brand-900/40 text-brand-500">{icon}</div>}
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">{title}</h1>
          {subtitle && <p className="text-sm text-slate-500 mt-0.5">{subtitle}</p>}
        </div>
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </motion.div>
  )
}
