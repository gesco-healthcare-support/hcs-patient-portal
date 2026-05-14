import type { FullAuditedEntityDto } from '@abp/ng.core';
import type { DocumentStatus } from './document-status.enum';
import type { PacketKind } from './packet-kind.enum';
import type { PacketGenerationStatus } from './packet-generation-status.enum';
import type { PatientPortalDocumentSource } from './patient-portal-document-source.enum';
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
  kind?: PacketKind;
  blobName?: string;
  status?: PacketGenerationStatus;
  generatedAt?: string;
  regeneratedAt?: string | null;
  errorMessage?: string | null;
}

export interface PatientPortalDocumentDto {
  id?: string;
  source?: PatientPortalDocumentSource;
  fileName?: string;
  contentType?: string;
  createdAt?: string;
  packetKind?: PacketKind | null;
  uploadStatus?: DocumentStatus | null;
}

export interface RejectDocumentInput {
  reason: string;
}

export interface UploadAppointmentDocumentForm {
  documentName?: string | null;
  file?: IFormFile;
}
