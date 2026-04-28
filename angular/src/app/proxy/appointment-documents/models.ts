import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface AppointmentDocumentDto extends FullAuditedEntityDto<string> {
  tenantId?: string;
  appointmentId: string;
  documentName: string;
  fileName: string;
  blobName: string;
  contentType?: string;
  fileSize: number;
  uploadedByUserId: string;
}
