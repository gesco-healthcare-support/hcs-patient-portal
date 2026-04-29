import type { FullAuditedEntityDto } from '@abp/ng.core';

export type PacketGenerationStatus = 1 | 2 | 3;
export const PacketGenerationStatus = {
  Generating: 1 as PacketGenerationStatus,
  Generated: 2 as PacketGenerationStatus,
  Failed: 3 as PacketGenerationStatus,
};

export interface AppointmentPacketDto extends FullAuditedEntityDto<string> {
  tenantId?: string;
  appointmentId: string;
  blobName: string;
  status: PacketGenerationStatus;
  generatedAt?: string;
  regeneratedAt?: string;
  errorMessage?: string;
}
