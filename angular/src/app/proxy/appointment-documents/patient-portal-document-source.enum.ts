import { mapEnumToOptions } from '@abp/ng.core';

export enum PatientPortalDocumentSource {
  Uploaded = 1,
  GeneratedPacket = 2,
}

export const patientPortalDocumentSourceOptions = mapEnumToOptions(PatientPortalDocumentSource);
