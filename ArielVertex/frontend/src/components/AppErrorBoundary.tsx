import { Component, ErrorInfo, ReactNode } from 'react'
import { AlertTriangle, Home, RefreshCw } from 'lucide-react'
import { Button, Card } from '../ui/primitives'

type Props = {
  children: ReactNode
  resetKey?: string
}

type State = {
  error: Error | null
}

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Ariel Vertex render error', error, info)
  }

  componentDidUpdate(prevProps: Props) {
    if (this.state.error && prevProps.resetKey !== this.props.resetKey) {
      this.setState({ error: null })
    }
  }

  private retry = () => this.setState({ error: null })

  render() {
    if (!this.state.error) return this.props.children

    return (
      <div className="min-h-screen grid place-items-center bg-slate-50 dark:bg-navy-900 px-4">
        <Card className="max-w-lg w-full p-6 text-center">
          <div className="mx-auto mb-4 grid h-12 w-12 place-items-center rounded-2xl bg-amber-50 text-amber-600 dark:bg-amber-900/30 dark:text-amber-300">
            <AlertTriangle className="h-6 w-6" />
          </div>
          <h1 className="text-lg font-extrabold tracking-tight">Something went wrong</h1>
          <p className="mt-2 text-sm text-slate-500">
            This page hit an unexpected rendering issue. You can retry the page or return to the dashboard.
          </p>
          <div className="mt-5 flex flex-wrap justify-center gap-2">
            <Button variant="secondary" icon={<RefreshCw className="h-4 w-4" />} onClick={this.retry}>Retry</Button>
            <Button icon={<Home className="h-4 w-4" />} onClick={() => { window.location.href = '/' }}>Go home</Button>
          </div>
        </Card>
      </div>
    )
  }
}