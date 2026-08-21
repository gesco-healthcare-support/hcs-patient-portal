import { mapEnumToOptions } from '@abp/ng.core';

export enum PacketGenerationStatus {
  Generating = 1,
  Generated = 2,
  Failed = 3,
}

export const packetGenerationStatusOptions = mapEnumToOptions(PacketGenerationStatus);
