import { ReactNode, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import { cx } from './util'

export function Modal({ open, onClose, title, subtitle, children, footer, size = 'md' }: {
  open: boolean; onClose: () => void; title: ReactNode; subtitle?: ReactNode
  children: ReactNode; footer?: ReactNode; size?: 'sm' | 'md' | 'lg' | 'xl'
}) {
  useEffect(() => {
    const h = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    if (open) window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [open, onClose])

  const widths = { sm: 'max-w-md', md: 'max-w-xl', lg: 'max-w-3xl', xl: 'max-w-5xl' }
  return createPortal(
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto p-4 sm:p-6">
          <motion.div className="fixed inset-0 bg-navy-900/50 backdrop-blur-sm"
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={onClose} />
          <motion.div
            className={cx('av-card relative z-10 w-full my-4 sm:my-10', widths[size])}
            initial={{ opacity: 0, y: 24, scale: 0.98 }} animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 16, scale: 0.98 }} transition={{ duration: 0.22, ease: 'easeOut' }}>
            <div className="flex items-start justify-between gap-4 px-6 pt-5 pb-4 border-b border-[var(--line)]">
              <div>
                <h2 className="text-lg font-bold">{title}</h2>
                {subtitle && <p className="text-sm text-slate-500 mt-0.5">{subtitle}</p>}
              </div>
              <button onClick={onClose} className="text-slate-400 hover:text-slate-600 rounded-lg p-1 -mr-1"><X className="h-5 w-5" /></button>
            </div>
            <div className="px-6 py-5">{children}</div>
            {footer && <div className="flex items-center justify-end gap-2 px-6 py-4 border-t border-[var(--line)]">{footer}</div>}
          </motion.div>
        </div>
      )}
    </AnimatePresence>,
    document.body,
  )
}
