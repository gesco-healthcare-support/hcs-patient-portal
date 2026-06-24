
export interface AppointmentDraftDto {
  payloadJson?: string;
  currentStep?: number;
  label?: string | null;
  lastSavedTime?: string;
}

export interface UpsertAppointmentDraftInput {
  payloadJson: string;
  currentStep?: number;
  label?: string | null;
}
