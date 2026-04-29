import type { FullAuditedEntityDto } from '@abp/ng.core';

export type DocumentStatus = 1 | 2 | 3;
export const DocumentStatus = {
  Uploaded: 1 as DocumentStatus,
  Approved: 2 as DocumentStatus,
  Rejected: 3 as DocumentStatus,
};

export interface AppointmentDocumentDto extends FullAuditedEntityDto<string> {
  tenantId?: string;
  appointmentId: string;
  documentName: string;
  fileName: string;
  blobName: string;
  contentType?: string;
  fileSize: number;
  uploadedByUserId: string;
  status: DocumentStatus;
  rejectionReason?: string;
  responsibleUserId?: string;
  rejectedByUserId?: string;
}

export interface RejectDocumentInput {
  reason: string;
}
