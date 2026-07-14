import { InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes, ReactNode } from 'react'
import { cx } from './util'

export function Field({ label, hint, error, children, required }: { label?: string; hint?: string; error?: string; children: ReactNode; required?: boolean }) {
  return (
    <label className="block">
      {label && <span className="av-label block mb-1.5">{label}{required && <span className="text-rose-500"> *</span>}</span>}
      {children}
      {error ? <span className="mt-1 block text-xs font-medium text-rose-500">{error}</span>
        : hint ? <span className="mt-1 block text-xs text-slate-400">{hint}</span> : null}
    </label>
  )
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={cx('av-input', props.className)} />
}
export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={cx('av-input min-h-[84px] resize-y', props.className)} />
}
export function Select({ children, ...props }: SelectHTMLAttributes<HTMLSelectElement> & { children: ReactNode }) {
  return <select {...props} className={cx('av-input appearance-none cursor-pointer', props.className)}>{children}</select>
}
