
export interface MyClaimExaminerProfileDto {
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  email?: string | null;
  concurrencyStamp?: string;
}

export interface UpdateMyClaimExaminerProfileInput {
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  concurrencyStamp?: string | null;
}
