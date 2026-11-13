/**
 * Hand-written twins of the C# DTOs in `Application.Contracts/ExternalLogins/ExternalLoginDtos.cs`
 * (2026-11-13). The generated proxy gains the same types the next time `abp generate-proxy` runs
 * against the API; until then these mirror the server exactly. Do not add fields the server lacks.
 */

/** The one permission behind the Outside logins screen. */
export const EXTERNAL_LOGINS_POLICY = 'CaseEvaluation.UserManagement.SwitchOffExternalUser';

/** Mirrors `GetExternalLoginsInput`. `isActive` null/undefined = every login. */
export interface GetExternalLoginsInput {
  filterText?: string;
  isActive?: boolean | null;
  skipCount: number;
  maxResultCount: number;
}

/** Mirrors `ExternalLoginDto`. `creationTime` is an instant (UTC, ends in Z). */
export interface ExternalLoginDto {
  id: string;
  name?: string | null;
  surname?: string | null;
  email?: string | null;
  roles: string[];
  firmName?: string | null;
  isActive: boolean;
  emailConfirmed: boolean;
  creationTime: string;
}

/** Mirrors `ExternalLoginSetActiveDto`. */
export interface ExternalLoginSetActiveDto {
  isActive: boolean;
}

/** Mirrors `PagedResultDto<ExternalLoginDto>`. */
export interface ExternalLoginPage {
  items: ExternalLoginDto[];
  totalCount: number;
}
