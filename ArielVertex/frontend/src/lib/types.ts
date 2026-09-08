// Mirrors the backend contract DTOs (kept intentionally light).

export interface CurrentUser {
  id: number
  name: string
  email: string
  employeeCode: string
  role: string
  roleLabel: string
  designation: string
  department?: string
  avatarColor: string
  dashboard: 'admin' | 'hr' | 'delivery' | 'business' | 'employee'
  permissions: string[]
}

export interface AuthResponse { token: string; expiresAt: string; user: CurrentUser }

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; totalPages: number }

export interface ProjectListItem {
  id: number; code: string; name: string; status: string; health: string; priority: string
  clientName: string; startDate: string; expectedEndDate?: string; memberCount: number
  tags: string[]; myRoleOnProject?: string
}

export interface ProjectMember {
  id: number; userId: number; name: string; email: string; designation: string
  avatarColor: string; roleOnProject: string; allocationPct: number; isActive: boolean
  startDate: string; endDate?: string
}

export interface ProjectDetail {
  id: number; code: string; name: string; description: string; status: string; health: string
  priority: string; startDate: string; expectedEndDate?: string; clientName: string
  businessOwner: string; tags: string[]; notes: string; members: ProjectMember[]
  canManage: boolean; myRoleOnProject?: string
}

export interface StatusUpdate {
  id: number; projectId: number; projectName: string; userId: number; userName: string
  avatarColor: string; updateDate: string; workCompleted: string; nextPlannedWork: string
  blockers: string; dependencies: string; hoursSpent: number; status: string
  internalNote?: string; clientShareableSummary: string; createdAt: string
}

export interface ReviewRequest {
  id: number; projectId: number; projectName: string; subjectUserId: number; subjectName: string
  avatarColor: string; requestedById: number; requestedByName: string; assignedToId?: number
  assignedToName?: string; reviewType: string; status: string; notes: string; dueDate?: string
  closedAt?: string; createdAt: string; meeting?: ReviewMeeting; hasOutcome: boolean
}
export type MeetingResponse = 'NoResponse' | 'Accepted' | 'Declined' | 'Tentative'
export interface ReviewMeeting {
  id: number; title: string; description: string; scheduledAt: string; durationMinutes: number
  attendees: string; teamsJoinUrl?: string; outlookEventId?: string; responseStatus: MeetingResponse
}

export interface CategoryScore { category: string; categoryName: string; score: number; notApplicable: boolean; comment: string }
export interface Feedback {
  id: number; subjectUserId: number; subjectName: string; avatarColor: string; authorId: number
  authorName: string; projectId?: number; projectName?: string; period: string; status: string
  statusLabel: string; constructiveSummary: string; strengths: string; improvementAreas: string
  actionPlan: string; internalNotes?: string; revisionReason?: string; createdAt: string
  approvedAt?: string; publishedAt?: string; categoryScores: CategoryScore[]; canApprove: boolean
}

export interface PerformanceReport {
  id: number; subjectUserId: number; subjectName: string; periodType: string; period: string
  overallScore: number; rating: string; ratingLabel: string; strengths: string
  improvementAreas: string; recommendedActions: string; dataSources: string; isPublished: boolean
  publishedAt?: string; categories: { category: string; categoryName: string; score: number; weight: number; notApplicable: boolean }[]
}
export interface MyPerformance {
  hasPublishedReport: boolean; latest?: PerformanceReport
  trend: { period: string; score: number; ratingLabel: string }[]
}

export interface NotificationItem { id: number; type: string; title: string; message: string; link?: string; isRead: boolean; createdAt: string }

export interface Stat { key: string; label: string; value: string; delta?: string; tone: string; icon: string }
export interface Dashboard {
  audience: string
  stats: Stat[]
  projectHealth: { projectId: number; code: string; name: string; health: string; status: string; missingUpdates: number }[]
  resourceOccupancy: { bucket: string; count: number }[]
  recentActivity: { icon: string; title: string; detail: string; when: string }[]
  upcoming: { kind: string; title: string; detail: string; when: string; link?: string }[]
  pending: { kind: string; title: string; detail: string; count: number; link?: string }[]
}

export interface UserListItem {
  id: number; name: string; email: string; employeeCode: string; role: string; roleLabel: string
  designation: string; departmentId?: number; department?: string; avatarColor: string; status: string
  managerId?: number; managerName?: string; joiningDate: string; skills: string
  isProvisionedFromEntra: boolean; profileManagedLocally: boolean; managerManagedLocally: boolean
  lastSyncedAt?: string
}

export interface ResourceRequestItem {
  id: number; projectId: number; projectName: string; requestedByName: string; roleTitle: string
  skills: string; reason: string; priority: string; count: number; expectedStartDate?: string
  status: string; statusLabel: string; createdAt: string
  comments: { id: number; authorName: string; message: string; createdAt: string }[]
}

export interface EnumOption { value: string; label: string }

// ---- Performance management (ported modules) ----
export interface Cycle {
  id: number; name: string; startDate: string; endDate: string
  status: 'Draft' | 'Active' | 'Closed'; appraisalCount: number; createdAt: string
}
export interface Appraisal {
  id: number; cycleId: number; cycleName: string; employeeId: number; employeeName: string; avatarColor: string
  managerId?: number; managerName?: string
  stage: 'SelfPending' | 'SelfSubmitted' | 'ManagerCompleted' | 'Released'
  selfRating?: number; selfComments?: string; selfSubmittedAt?: string
  managerRating?: number; managerComments?: string; managerReviewedAt?: string
  finalRating?: number; releasedAt?: string; createdAt: string
}
export interface Goal {
  id: number; employeeId: number; employeeName: string; avatarColor: string; title: string; description: string
  category: string; weightage: number; progress: number; targetDate: string
  status: 'NotStarted' | 'InProgress' | 'Completed' | 'Cancelled'
  cycleId?: number; assignedByName?: string; createdAt: string
}
export type PromotionStage = 'ManagerRecommended' | 'HrValidated' | 'LeadershipApproved' | 'Completed' | 'Rejected'
export type RecommendationType = 'Promotion' | 'Hike' | 'Both'
export interface Promotion {
  id: number; employeeId: number; employeeName: string; avatarColor: string
  currentDesignation: string; proposedDesignation: string; currentSalary?: number; proposedSalary?: number
  justification: string; stage: PromotionStage; recommendationType: RecommendationType; decisionNote?: string; recommendedByName?: string
  validatedAt?: string; approvedAt?: string; completedAt?: string; createdAt: string
}
export interface Training {
  id: number; employeeId: number; employeeName: string; avatarColor: string; skillGap: string
  recommendedTraining: string; durationMonths: number
  status: 'Recommended' | 'InProgress' | 'Completed'; source: string; createdAt: string
}
export interface SearchResult { type: string; id: number; title: string; subtitle: string; link: string }

export interface ExpenseItem {
  id: number; title: string; description: string; category: string; categoryLabel: string
  amount: number; currency: string; vendor: string; expenseDate: string; paymentMethod: string
  invoiceNumber?: string; hasInvoiceFile: boolean; invoiceFileName?: string
  status: string; statusLabel: string; approvalRequired: boolean
  raisedById: number; raisedByName: string; approverName?: string; decidedAt?: string
  decisionNote?: string; paidAt?: string; createdAt: string; canApprove: boolean; canManage: boolean
}
export interface ExpenseSummary { total: number; pendingApproval: number; paid: number; totalAmount: number; paidAmount: number }
export interface ExpenseSettings {
  approvalRequiredByDefault: boolean; approverRoles: string[]
  dailySummaryRecipients: string; weeklySummaryRecipients: string; teamsWebhookUrl?: string
}

export type PettyCashEntryStatus = 'Pending' | 'Approved' | 'Rejected'
export interface PettyCashItem {
  id: number; date: string; particulars: string
  openingBalance: number; credit: number; debit: number; balance: number
  notes?: string; createdByName: string; createdAt: string
  status: PettyCashEntryStatus; canManage: boolean
}

export interface BillItem {
  id: number; title: string; description: string; category: string
  amount: number; currency: string; vendor: string; billDate: string; dueDate?: string; paymentMethod: string
  invoiceNumber?: string; hasInvoiceFile: boolean; invoiceFileName?: string
  status: string; statusLabel: string; approvalRequired: boolean
  raisedById: number; raisedByName: string; approverName?: string; decidedAt?: string
  decisionNote?: string; paidAt?: string; createdAt: string; canApprove: boolean; canManage: boolean
  daysUntilDue: number
}
export interface BillSummary { total: number; pendingApproval: number; paid: number; overdue: number; totalAmount: number; paidAmount: number }
export interface BillSettings {
  approvalRequiredByDefault: boolean; approverRoles: string[]
  dailySummaryRecipients: string; weeklySummaryRecipients: string
  reminderDaysBefore: string; reminderRecipients: string; teamsWebhookUrl?: string
}
export interface ExtractedBillData {
  vendor?: string; amount?: number; dueDate?: string; billDate?: string; category?: string; invoiceNumber?: string
}
