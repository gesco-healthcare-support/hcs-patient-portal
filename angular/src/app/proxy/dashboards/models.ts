
export interface DashboardActivityItemDto {
  icon?: string;
  tint?: string;
  text?: string;
  when?: string;
}

export interface DashboardCountersDto {
  pendingRequests?: number;
  approvedThisWeek?: number;
  rejectedThisWeek?: number;
  pendingChangeRequests?: number;
  requestsApproachingLegalDeadline?: number;
  decisionOverdue?: number;
  billedThisMonth?: number;
  noShowThisMonth?: number;
  rescheduledThisMonth?: number;
  cancelledThisWeek?: number;
  checkedInToday?: number;
  checkedOutToday?: number;
  totalDoctors?: number;
  totalTenants?: number;
}

export interface DashboardDeadlineItemDto {
  appointmentId?: string;
  confirmationNumber?: string;
  patientName?: string;
  requestedAt?: string;
  dueDate?: string;
  daysRemaining?: number;
}

export interface DashboardDto {
  isHost?: boolean;
  pendingRequests?: DashboardKpiDto;
  pendingChangeRequests?: DashboardKpiDto;
  approvedRequests?: DashboardKpiDto;
  rejectedRequests?: DashboardKpiDto;
  totalTenants?: number;
  totalDoctors?: number;
  totalAppointments?: number;
  pendingAcrossTenants?: number;
  deadlines?: DashboardDeadlineItemDto[];
  deadlineApproachingCount?: number;
  trend?: DashboardTrendPointDto[];
  statusBreakdown?: DashboardStatusSliceDto[];
  todaySchedule?: DashboardScheduleItemDto[];
  recentActivity?: DashboardActivityItemDto[];
  tenants?: DashboardTenantRowDto[];
}

export interface DashboardKpiDto {
  value?: number;
  previousValue?: number;
}

export interface DashboardScheduleItemDto {
  appointmentDate?: string;
  appointmentType?: string;
  location?: string;
}

export interface DashboardStatusSliceDto {
  pill?: string;
  count?: number;
}

export interface DashboardTenantRowDto {
  tenantName?: string;
  appointments?: number;
  pending?: number;
  approved?: number;
  thisWeek?: number;
}

export interface DashboardTrendPointDto {
  label?: string;
  weekStart?: string;
  count?: number;
}

export interface TenantSummaryDto {
  tenantId?: string;
  name?: string;
  userCount?: number;
  appointmentCount?: number;
}
