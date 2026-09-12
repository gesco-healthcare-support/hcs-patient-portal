import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ClaimTypeDto, DownloadTokenResultDto, ExternalLoginProviderDto, GetIdentityUserListAsFileInput, GetIdentityUsersInput, GetImportInvalidUsersFileInput, GetImportUsersSampleFileInput, IdentityRoleDto, IdentityRoleLookupDto, IdentityUserClaimDto, IdentityUserCreateDto, IdentityUserDto, IdentityUserUpdateDto, IdentityUserUpdatePasswordInput, IdentityUserUpdateRolesDto, ImportExternalUserInput, ImportUsersFromFileInputWithStream, ImportUsersFromFileOutput, OrganizationUnitDto, OrganizationUnitLookupDto, OrganizationUnitWithDetailsDto } from '../volo/abp/identity/models';

@Injectable({
  providedIn: 'root',
})
export class UserExtendedService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: IdentityUserCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: '/api/app/user-extended',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/user-extended/${id}`,
    },
    { apiName: this.apiName,...config });
  

  findByEmail = (email: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: '/api/app/user-extended/find-by-email',
      params: { email },
    },
    { apiName: this.apiName,...config });
  

  findById = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: `/api/app/user-extended/${id}/find-by-id`,
    },
    { apiName: this.apiName,...config });
  

  findByUsername = (username: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: '/api/app/user-extended/find-by-username',
      params: { username },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'GET',
      url: `/api/app/user-extended/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAllClaimTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClaimTypeDto[]>({
      method: 'GET',
      url: '/api/app/user-extended/claim-types',
    },
    { apiName: this.apiName,...config });
  

  getAssignableRoles = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<IdentityRoleDto>>({
      method: 'GET',
      url: '/api/app/user-extended/assignable-roles',
    },
    { apiName: this.apiName,...config });
  

  getAvailableOrganizationUnits = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<OrganizationUnitWithDetailsDto>>({
      method: 'GET',
      url: '/api/app/user-extended/available-organization-units',
    },
    { apiName: this.apiName,...config });
  

  getClaims = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserClaimDto[]>({
      method: 'GET',
      url: `/api/app/user-extended/${id}/claims`,
    },
    { apiName: this.apiName,...config });
  

  getDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>({
      method: 'GET',
      url: '/api/app/user-extended/download-token',
    },
    { apiName: this.apiName,...config });
  

  getExternalLoginProviders = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExternalLoginProviderDto[]>({
      method: 'GET',
      url: '/api/app/user-extended/external-login-providers',
    },
    { apiName: this.apiName,...config });
  

  getImportInvalidUsersFile = (input: GetImportInvalidUsersFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/user-extended/import-invalid-users-file',
      params: { token: input.token },
    },
    { apiName: this.apiName,...config });
  

  getImportUsersSampleFile = (input: GetImportUsersSampleFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/user-extended/import-users-sample-file',
      params: { fileType: input.fileType, token: input.token },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIdentityUsersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IdentityUserDto>>({
      method: 'GET',
      url: '/api/app/user-extended',
      params: { filter: input.filter, roleId: input.roleId, organizationUnitId: input.organizationUnitId, id: input.id, userName: input.userName, phoneNumber: input.phoneNumber, emailAddress: input.emailAddress, name: input.name, surname: input.surname, isLockedOut: input.isLockedOut, notActive: input.notActive, emailConfirmed: input.emailConfirmed, isExternal: input.isExternal, maxCreationTime: input.maxCreationTime, minCreationTime: input.minCreationTime, maxModifitionTime: input.maxModifitionTime, minModifitionTime: input.minModifitionTime, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, extraProperties: input.extraProperties },
    },
    { apiName: this.apiName,...config });
  

  getListAsCsvFile = (input: GetIdentityUserListAsFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/user-extended/as-csv-file',
      params: { token: input.token, filter: input.filter, roleId: input.roleId, organizationUnitId: input.organizationUnitId, id: input.id, userName: input.userName, phoneNumber: input.phoneNumber, emailAddress: input.emailAddress, name: input.name, surname: input.surname, isLockedOut: input.isLockedOut, notActive: input.notActive, emailConfirmed: input.emailConfirmed, isExternal: input.isExternal, maxCreationTime: input.maxCreationTime, minCreationTime: input.minCreationTime, maxModifitionTime: input.maxModifitionTime, minModifitionTime: input.minModifitionTime, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, extraProperties: input.extraProperties },
    },
    { apiName: this.apiName,...config });
  

  getListAsExcelFile = (input: GetIdentityUserListAsFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/user-extended/as-excel-file',
      params: { token: input.token, filter: input.filter, roleId: input.roleId, organizationUnitId: input.organizationUnitId, id: input.id, userName: input.userName, phoneNumber: input.phoneNumber, emailAddress: input.emailAddress, name: input.name, surname: input.surname, isLockedOut: input.isLockedOut, notActive: input.notActive, emailConfirmed: input.emailConfirmed, isExternal: input.isExternal, maxCreationTime: input.maxCreationTime, minCreationTime: input.minCreationTime, maxModifitionTime: input.maxModifitionTime, minModifitionTime: input.minModifitionTime, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, extraProperties: input.extraProperties },
    },
    { apiName: this.apiName,...config });
  

  getOrganizationUnitLookup = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OrganizationUnitLookupDto[]>({
      method: 'GET',
      url: '/api/app/user-extended/organization-unit-lookup',
    },
    { apiName: this.apiName,...config });
  

  getOrganizationUnits = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OrganizationUnitDto[]>({
      method: 'GET',
      url: `/api/app/user-extended/${id}/organization-units`,
    },
    { apiName: this.apiName,...config });
  

  getRoleLookup = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityRoleLookupDto[]>({
      method: 'GET',
      url: '/api/app/user-extended/role-lookup',
    },
    { apiName: this.apiName,...config });
  

  getRoles = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<IdentityRoleDto>>({
      method: 'GET',
      url: `/api/app/user-extended/${id}/roles`,
    },
    { apiName: this.apiName,...config });
  

  getTwoFactorEnabled = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'GET',
      url: `/api/app/user-extended/${id}/two-factor-enabled`,
    },
    { apiName: this.apiName,...config });
  

  importExternalUser = (input: ImportExternalUserInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'POST',
      url: '/api/app/user-extended/import-external-user',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  importUsersFromFile = (input: ImportUsersFromFileInputWithStream, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ImportUsersFromFileOutput>({
      method: 'POST',
      url: '/api/app/user-extended/import-users-from-file',
      params: { fileType: input.fileType },
      body: input.file,
    },
    { apiName: this.apiName,...config });
  

  lock = (id: string, lockoutEnd: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/user-extended/${id}/lock`,
      params: { lockoutEnd },
    },
    { apiName: this.apiName,...config });
  

  setTwoFactorEnabled = (id: string, enabled: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/user-extended/${id}/set-two-factor-enabled`,
      params: { enabled },
    },
    { apiName: this.apiName,...config });
  

  unlock = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/user-extended/${id}/unlock`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: IdentityUserUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IdentityUserDto>({
      method: 'PUT',
      url: `/api/app/user-extended/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateClaims = (id: string, input: IdentityUserClaimDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/user-extended/${id}/claims`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updatePassword = (id: string, input: IdentityUserUpdatePasswordInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/user-extended/${id}/password`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateRoles = (id: string, input: IdentityUserUpdateRolesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/user-extended/${id}/roles`,
      body: input,
    },
    { apiName: this.apiName,...config });
}