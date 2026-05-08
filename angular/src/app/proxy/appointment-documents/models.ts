import type { FullAuditedEntityDto } from '@abp/ng.core';
import type { DocumentStatus } from './document-status.enum';
import type { PacketGenerationStatus } from './packet-generation-status.enum';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

export interface AppointmentDocumentDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  appointmentId?: string;
  documentName?: string;
  fileName?: string;
  blobName?: string;
  contentType?: string | null;
  fileSize?: number;
  uploadedByUserId?: string;
  status?: DocumentStatus;
  rejectionReason?: string | null;
  responsibleUserId?: string | null;
  rejectedByUserId?: string | null;
}

export interface AppointmentPacketDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  appointmentId?: string;
  blobName?: string;
  status?: PacketGenerationStatus;
  generatedAt?: string;
  regeneratedAt?: string | null;
  errorMessage?: string | null;
}

export interface RejectDocumentInput {
  reason: string;
}

export interface UploadAppointmentDocumentForm {
  documentName?: string | null;
  file?: IFormFile;
}
