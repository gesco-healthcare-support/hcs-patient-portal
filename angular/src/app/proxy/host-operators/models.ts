
export interface AssignIntakeOfficeDto {
  operatorUserId: string;
  officeId: string;
}

export interface IntakeOfficeAssignmentDto {
  id?: string;
  operatorUserId?: string;
  operatorName?: string;
  operatorEmail?: string;
  officeId?: string;
  officeName?: string;
}
