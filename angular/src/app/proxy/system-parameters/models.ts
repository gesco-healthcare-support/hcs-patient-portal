import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface SystemParameterDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  appointmentLeadTime?: number;
  appointmentMaxTimePQME?: number;
  appointmentMaxTimeAME?: number;
  appointmentMaxTimeOTHER?: number;
  appointmentCancelTime?: number;
  appointmentDueDays?: number;
  appointmentDurationTime?: number;
  autoCancelCutoffTime?: number;
  jointDeclarationUploadCutoffDays?: number;
  pendingAppointmentOverDueNotificationDays?: number;
  reminderCutoffTime?: number;
  isCustomField?: boolean;
  ccEmailIds?: string | null;
  concurrencyStamp?: string;
}

export interface SystemParameterUpdateDto {
  appointmentLeadTime?: number;
  appointmentMaxTimePQME?: number;
  appointmentMaxTimeAME?: number;
  appointmentMaxTimeOTHER?: number;
  appointmentCancelTime?: number;
  appointmentDueDays?: number;
  appointmentDurationTime?: number;
  autoCancelCutoffTime?: number;
  jointDeclarationUploadCutoffDays?: number;
  pendingAppointmentOverDueNotificationDays?: number;
  reminderCutoffTime?: number;
  isCustomField?: boolean;
  ccEmailIds?: string | null;
  concurrencyStamp?: string;
}
