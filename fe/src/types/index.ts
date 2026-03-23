export interface User {
  id: string
  email: string
  fullName: string
  phoneNumber?: string
  rankELO: number
  walletBalance: number
  roles: string[]
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  confirmPassword: string
  fullName: string
  phoneNumber?: string
}

export interface WalletSummary {
  memberId: string
  memberName: string
  balance: number
  totalDeposits: number
  totalWithdrawals: number
}

export interface Transaction {
  id: string
  memberId: string
  category: TransactionCategory
  amount: number
  transType: TransactionType
  refId?: string
  status: TransactionStatus
  description?: string
  createdAt: string
}

export enum TransactionCategory {
  Deposit = 0,
  SubcontractorPayment = 1,
  MaterialPurchase = 2,
  Refund = 3
}

export enum TransactionType {
  Credit = 0,
  Debit = 1
}

export enum TransactionStatus {
  Pending = 0,
  Success = 1,
  Failed = 2,
  Cancelled = 3
}

export interface Project {
  id: string
  name: string
  clientId: string
  clientName: string
  managerId: string
  managerName: string
  startDate: string
  targetDate?: string
  totalBudget: number
  walletBalance: number
  spentBudget: number
  status: ProjectStatus
  taskCount: number
  completedTaskCount: number
  progressPercentage: number
}

export enum ProjectStatus {
  Planning = 0,
  Ongoing = 1,
  Handover = 2,
  Completed = 3
}

export interface ProjectTask {
  id: number
  projectId: string
  projectName: string
  phaseType: PhaseType
  name: string
  subcontractorId?: string
  subcontractorName?: string
  startTime?: string
  endTime?: string
  targetDate?: string
  progressPct: number
  status: TaskStatus
  estimatedCost: number
  imageUrl?: string
  approvedBy?: string
  approvedAt?: string
}

export enum PhaseType {
  Demolition = 0,
  MEP = 1,
  Drywall = 2,
  Painting = 3,
  Woodwork = 4
}

export enum TaskStatus {
  ToDo = 0,
  InProgress = 1,
  Review = 2,
  Approved = 3,
  Blocked = 4
}

export interface Booking {
  id: string
  resourceId: number
  resourceName: string
  memberId: string
  memberName: string
  startTime: string
  endTime: string
  price: number
  status: BookingStatus
  groupId?: string
  createdAt: string
}

export enum BookingStatus {
  PendingPayment = 0,
  Confirmed = 1,
  Cancelled = 2,
  InUse = 3
}

export interface Resource {
  id: number
  name: string
  description?: string
  hourlyRate?: number
  isActive: boolean
}

export interface TimeSlot {
  startTime: string
  endTime: string
  isAvailable: boolean
  bookingId?: string
}

export interface LeaderboardEntry {
  rank: number
  memberId: string
  memberName: string
  eloScore: number
}

export interface ApiResponse<T> {
  success: boolean
  message?: string
  data?: T
  errors?: string[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}
