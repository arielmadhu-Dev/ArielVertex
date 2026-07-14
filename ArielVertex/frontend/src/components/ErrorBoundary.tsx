import { Component, ErrorInfo, ReactNode } from 'react'
import { AlertTriangle, RotateCcw } from 'lucide-react'

interface Props {
  children: ReactNode
  /** When this value changes (e.g. the route path), the boundary resets and retries rendering. */
  resetKey?: string
}
interface State { error: Error | null }

/**
 * Catches render/runtime errors in the subtree and shows a recoverable fallback instead of a
 * blank white screen. Resets automatically when `resetKey` changes so navigating away from a
 * crashed page restores the app without a full reload.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidUpdate(prev: Props) {
    if (prev.resetKey !== this.props.resetKey && this.state.error) {
      this.setState({ error: null })
    }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Surface for diagnostics; a real telemetry sink can hook in here later.
    console.error('[ErrorBoundary] render error:', error, info.componentStack)
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <div className="grid place-items-center py-20 px-4">
        <div className="av-card max-w-md w-full p-8 text-center">
          <div className="mx-auto mb-4 grid h-12 w-12 place-items-center rounded-2xl bg-rose-50 text-rose-600 dark:bg-rose-900/30">
            <AlertTriangle className="h-6 w-6" />
          </div>
          <h2 className="text-lg font-bold">This page hit a snag</h2>
          <p className="mt-1.5 text-sm text-slate-500">
            Something went wrong while rendering this view. You can retry, or use the menu to move on —
            the rest of the app is still working.
          </p>
          <button
            onClick={() => this.setState({ error: null })}
            className="mt-5 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700">
            <RotateCcw className="h-4 w-4" /> Retry
          </button>
        </div>
      </div>
    )
  }
}
