
export interface DashboardCountersDto {
  pendingRequests?: number;
  approvedThisWeek?: number;
  rejectedThisWeek?: number;
  pendingChangeRequests?: number;
  requestsApproachingLegalDeadline?: number;
  billedThisMonth?: number;
  noShowThisMonth?: number;
  rescheduledThisMonth?: number;
  cancelledThisWeek?: number;
  checkedInToday?: number;
  checkedOutToday?: number;
  totalDoctors?: number;
  totalTenants?: number;
}
