import type { IFormFile } from '../microsoft/asp-net-core/http/models';

export interface UploadUserSignatureForm {
  file?: IFormFile;
}

export interface UserSignatureInfoDto {
  hasSignature?: boolean;
  fileName?: string | null;
  contentType?: string | null;
}
