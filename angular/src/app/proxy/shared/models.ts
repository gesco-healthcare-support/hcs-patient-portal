import type { PagedResultRequestDto } from '@abp/ng.core';

export interface DownloadTokenResultDto {
  token?: string;
}

export interface LookupDto<TKey> {
  id?: TKey | null;
  displayName?: string;
}

export interface LookupRequestDto extends PagedResultRequestDto {
  filter?: string | null;
}
