import { useQuery } from '@tanstack/react-query'
import { api } from './api'

// Role-based appraisal forms. HR configures two independent forms per role — the employee's
// reflective Self assessment and the reporting manager's evaluation scorecard.

export type FormVariant = 'Self' | 'Manager'
export type AreaType = 'Rating' | 'Text'

export interface FormArea { name: string; type: AreaType; weight: number; allowNa: boolean }
export interface AppraisalForm { id: number; role: string; variant: FormVariant; weighted: boolean; areas: FormArea[] }
export interface RolesResponse { roles: string[]; selfWeightPct: number }

/** One rating/answer sent back for a form area. */
export interface AreaScoreInput { areaName: string; rating?: number | null; comment?: string | null; notApplicable?: boolean }
export interface AreaScore { areaName: string; stage: FormVariant; rating?: number | null; comment?: string | null; notApplicable: boolean }
export interface ScoreSummary {
  selfScore?: number | null; managerScore?: number | null; blendedScore?: number | null
  selfWeightPct: number; managerWeightPct: number; areas: AreaScore[]
}

export const RATING_SCALE = [
  { v: 5, label: 'Outstanding', hint: 'Consistently exceeds expectations' },
  { v: 4, label: 'Exceeds Expectations', hint: 'Frequently performs above expectations' },
  { v: 3, label: 'Meets Expectations', hint: 'Consistently meets job requirements' },
  { v: 2, label: 'Needs Improvement', hint: 'Performance is below expectations in some areas' },
  { v: 1, label: 'Unsatisfactory', hint: 'Performance is significantly below expectations' },
]

export const band = (score: number) =>
  score >= 90 ? 'Outstanding' : score >= 80 ? 'Exceeds Expectations'
    : score >= 70 ? 'Meets Expectations' : score >= 60 ? 'Needs Improvement' : 'Unsatisfactory'

export const bandTone = (score: number): 'good' | 'brand' | 'warn' | 'danger' =>
  score >= 80 ? 'good' : score >= 70 ? 'brand' : score >= 60 ? 'warn' : 'danger'

/** Mirrors the server's scoring so the form can show a live total before submitting. */
export function localScore(areas: FormArea[], weighted: boolean, answers: Record<string, AreaScoreInput>) {
  const usable = areas.filter(
    (a) => a.type === 'Rating' && !answers[a.name]?.notApplicable && !!answers[a.name]?.rating)
  if (!usable.length) return null
  if (!weighted) return Math.round(usable.reduce((s, a) => s + (answers[a.name].rating || 0), 0) / usable.length / 5 * 100)
  const total = usable.reduce((s, a) => s + a.weight, 0)
  if (total <= 0) return Math.round(usable.reduce((s, a) => s + (answers[a.name].rating || 0), 0) / usable.length / 5 * 100)
  return Math.round(usable.reduce((s, a) => s + (answers[a.name].rating || 0) / 5 * 100 * (a.weight / total), 0))
}

export function useFormRoles() {
  return useQuery({
    queryKey: ['appraisal-form-roles'],
    queryFn: async () => (await api.get<RolesResponse>('/appraisal-forms/roles')).data,
  })
}

export function useAppraisalForm(role: string | undefined, variant: FormVariant, enabled = true) {
  return useQuery({
    queryKey: ['appraisal-form', role, variant],
    enabled: !!role && enabled,
    queryFn: async () => (await api.get<AppraisalForm>('/appraisal-forms', { params: { role, variant } })).data,
  })
}

export function useAppraisalScores(appraisalId: number | undefined) {
  return useQuery({
    queryKey: ['appraisal-scores', appraisalId],
    enabled: !!appraisalId,
    queryFn: async () => (await api.get<ScoreSummary>(`/appraisals/${appraisalId}/scores`)).data,
  })
}
