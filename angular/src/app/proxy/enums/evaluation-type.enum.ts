import { mapEnumToOptions } from '@abp/ng.core';

export enum EvaluationType {
  Normal = 0,
  Re = 1,
  Both = 2,
}

export const evaluationTypeOptions = mapEnumToOptions(EvaluationType);
