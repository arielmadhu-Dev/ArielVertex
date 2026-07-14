import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Award, Send, CheckCircle2, Star } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { Appraisal, Cycle } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, StatusPill, Avatar, Button, EmptyState } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { P } from '../components/nav'

type Action = { appraisal: Appraisal; kind: 'self' | 'manager' | 'release' }

export default function Appraisals() {
  const { user, has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const [cycleId, setCycleId] = useState<number | 0>(0)
  const [action, setAction] = useState<Action | null>(null)

  const cycles = useQuery({ queryKey: ['cycles'], queryFn: async () => (await api.get<Cycle[]>('/cycles')).data })
  const { data } = useQuery({
    queryKey: ['appraisals', cycleId],
    queryFn: async () => (await api.get<Appraisal[]>('/appraisals', { params: cycleId ? { cycleId } : {} })).data,
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['appraisals'] })

  const canRelease = has(P.AppraisalsRelease)
  const canManage = has(P.AppraisalsManage) || canRelease

  return (
    <div>
      <PageHeader title="Appraisals" subtitle="Self assessment → manager evaluation → final rating release" icon={<Award className="h-5 w-5" />}
        actions={
          <Select value={cycleId} onChange={(e) => setCycleId(Number(e.target.value))} className="w-52">
            <option value={0}>All cycles</option>
            {cycles.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        } />

      {data?.length ? (
        <div className="space-y-3">
          {data.map((a) => {
            const isOwner = a.employeeId === user?.id
            const isManager = a.managerId === user?.id
            const doSelf = isOwner && a.stage === 'SelfPending'
            const doManager = (isManager || canManage) && a.stage === 'SelfSubmitted'
            const doRelease = canRelease && a.stage === 'ManagerCompleted'
            return (
              <Card key={a.id} className="p-4 flex flex-wrap items-center gap-4">
                <Avatar name={a.employeeName} color={a.avatarColor} size={42} />
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="font-bold">{a.employeeName}</p>
                    <StatusPill value={a.stage} />
                    <Badge t="info">{a.cycleName}</Badge>
                  </div>
                  <p className="text-xs text-slate-500 mt-0.5">
                    {a.managerName ? `Manager: ${a.managerName}` : 'No manager assigned'}
                    {a.selfRating != null && ` · Self ${a.selfRating.toFixed(1)}`}
                    {a.managerRating != null && ` · Manager ${a.managerRating.toFixed(1)}`}
                    {a.finalRating != null && ` · Final ${a.finalRating.toFixed(1)}`}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {a.stage === 'Released' && <span className="inline-flex items-center gap-1 text-emerald-600 text-sm font-semibold"><CheckCircle2 className="h-4 w-4" />{a.finalRating?.toFixed(1)} / 5</span>}
                  {doSelf && <Button size="sm" icon={<Send className="h-4 w-4" />} onClick={() => setAction({ appraisal: a, kind: 'self' })}>Submit self</Button>}
                  {doManager && <Button size="sm" icon={<Star className="h-4 w-4" />} onClick={() => setAction({ appraisal: a, kind: 'manager' })}>Evaluate</Button>}
                  {doRelease && <Button size="sm" variant="secondary" icon={<CheckCircle2 className="h-4 w-4" />} onClick={() => setAction({ appraisal: a, kind: 'release' })}>Release</Button>}
                </div>
              </Card>
            )
          })}
        </div>
      ) : <Card><EmptyState icon={<Award className="h-6 w-6" />} title="No appraisals" hint="Appraisals appear once HR activates a cycle." /></Card>}

      {action && <AppraisalModal action={action} onClose={() => setAction(null)} onDone={() => { invalidate(); setAction(null) }} push={push} />}
    </div>
  )
}

function AppraisalModal({ action, onClose, onDone, push }: { action: Action; onClose: () => void; onDone: () => void; push: any }) {
  const { appraisal: a, kind } = action
  const [rating, setRating] = useState(kind === 'release' ? (a.managerRating ?? 3) : 3.5)
  const [comments, setComments] = useState('')

  const submit = useMutation({
    mutationFn: () => {
      if (kind === 'self') return api.post(`/appraisals/${a.id}/self`, { selfRating: rating, selfComments: comments })
      if (kind === 'manager') return api.post(`/appraisals/${a.id}/manager`, { managerRating: rating, managerComments: comments })
      return api.post(`/appraisals/${a.id}/release`, { finalRating: rating })
    },
    onSuccess: () => { push(kind === 'self' ? 'Self assessment submitted' : kind === 'manager' ? 'Evaluation submitted' : 'Rating released'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })

  const title = kind === 'self' ? 'Submit self assessment' : kind === 'manager' ? 'Manager evaluation' : 'Release final rating'
  return (
    <Modal open onClose={onClose} title={title} subtitle={`${a.employeeName} · ${a.cycleName}`}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={submit.isPending} onClick={() => submit.mutate()}>Submit</Button></>}>
      <div className="space-y-4">
        {kind === 'manager' && a.selfComments && (
          <div className="rounded-xl bg-slate-50 dark:bg-navy-600 p-3 text-sm">
            <p className="text-xs font-semibold text-slate-500 mb-1">Employee self assessment ({a.selfRating?.toFixed(1)}/5)</p>
            {a.selfComments}
          </div>
        )}
        <Field label="Rating (0–5)" required>
          <Input type="number" min={0} max={5} step={0.5} value={rating} onChange={(e) => setRating(Number(e.target.value))} />
        </Field>
        {kind !== 'release' && <Field label="Comments"><Textarea value={comments} onChange={(e) => setComments(e.target.value)} placeholder={kind === 'self' ? 'Highlights, challenges, growth…' : 'Evaluation notes…'} /></Field>}
      </div>
    </Modal>
  )
}
