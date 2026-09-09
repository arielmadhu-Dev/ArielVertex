import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { EnumOption, UserListItem, Paged } from './types'

interface Enums {
  roles: EnumOption[]; employeeStatuses: EnumOption[]; projectRoles: EnumOption[]; projectStatus: EnumOption[]; projectHealth: EnumOption[]
  priorities: EnumOption[]; reviewTypes: EnumOption[]; resourceStatuses: EnumOption[]
  documentCategories: EnumOption[]; visibilities: EnumOption[]; updateStatuses: EnumOption[]; callTypes: EnumOption[]
  expenseCategories: EnumOption[]; expenseStatuses: EnumOption[]
  cycleStatuses: EnumOption[]; appraisalStages: EnumOption[]; goalStatuses: EnumOption[]
  promotionStages: EnumOption[]; trainingStatuses: EnumOption[]; goalCategories: EnumOption[]
  pettyCashStatuses: EnumOption[]
  helpdeskTicketStatuses: EnumOption[]; helpdeskTicketPriorities: EnumOption[]; helpdeskTicketCategories: EnumOption[]
  assetCategories: EnumOption[]; assetConditions: EnumOption[]; assetStatuses: EnumOption[]
  assetRequestTypes: EnumOption[]; assetRequestStatuses: EnumOption[]
}

export function useEnums() {
  return useQuery({
    queryKey: ['enums'],
    queryFn: async () => (await api.get<Enums>('/meta/enums')).data,
    staleTime: Infinity,
  })
}

export function useFeatures() {
  return useQuery({
    queryKey: ['features'],
    queryFn: async () => (await api.get<Record<string, boolean>>('/config/features')).data,
    staleTime: 30_000,
  })
}

export function useEmployees() {
  return useQuery({
    queryKey: ['employees', 'all'],
    queryFn: async () => (await api.get<Paged<UserListItem>>('/employees', { params: { pageSize: 100 } })).data.items,
    staleTime: 60_000,
  })
}
