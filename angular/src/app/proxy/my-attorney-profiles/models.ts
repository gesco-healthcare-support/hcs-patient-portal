
export interface MyAttorneyProfileDto {
  kind?: string;
  firstName?: string | null;
  lastName?: string | null;
  firmName?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  email?: string | null;
  concurrencyStamp?: string;
}

export interface UpdateMyAttorneyProfileInput {
  firstName?: string | null;
  lastName?: string | null;
  firmName?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  concurrencyStamp?: string | null;
}
