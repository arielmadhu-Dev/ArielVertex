import { createContext, useCallback, useContext, useState, ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { AnimatePresence, motion } from 'framer-motion'
import { CheckCircle2, AlertTriangle, Info, X } from 'lucide-react'

type Kind = 'success' | 'error' | 'info'
interface Toast { id: number; kind: Kind; message: string }
interface Ctx { push: (message: string, kind?: Kind) => void }

const ToastContext = createContext<Ctx>({ push: () => {} })
export const useToast = () => useContext(ToastContext)

const icons = {
  success: <CheckCircle2 className="h-5 w-5 text-emerald-500" />,
  error: <AlertTriangle className="h-5 w-5 text-rose-500" />,
  info: <Info className="h-5 w-5 text-brand-500" />,
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const push = useCallback((message: string, kind: Kind = 'success') => {
    const id = Date.now() + Math.random()
    setToasts((t) => [...t, { id, kind, message }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 4000)
  }, [])

  return (
    <ToastContext.Provider value={{ push }}>
      {children}
      {createPortal(
        <div className="fixed bottom-5 right-5 z-[60] flex flex-col gap-2 w-[min(92vw,360px)]">
          <AnimatePresence>
            {toasts.map((t) => (
              <motion.div key={t.id} layout
                initial={{ opacity: 0, x: 40, scale: 0.95 }} animate={{ opacity: 1, x: 0, scale: 1 }}
                exit={{ opacity: 0, x: 40, scale: 0.95 }} transition={{ duration: 0.22 }}
                className="av-card shadow-pop flex items-start gap-3 px-4 py-3">
                {icons[t.kind]}
                <span className="text-sm font-medium flex-1">{t.message}</span>
                <button onClick={() => setToasts((x) => x.filter((y) => y.id !== t.id))} className="text-slate-400 hover:text-slate-600"><X className="h-4 w-4" /></button>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>,
        document.body,
      )}
    </ToastContext.Provider>
  )
}
