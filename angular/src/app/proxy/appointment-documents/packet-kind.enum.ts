import { mapEnumToOptions } from '@abp/ng.core';

export enum PacketKind {
  Patient = 1,
  Doctor = 2,
  AttorneyClaimExaminer = 3,
}

export const packetKindOptions = mapEnumToOptions(PacketKind);
