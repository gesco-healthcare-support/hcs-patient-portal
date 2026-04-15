import { ABP, eLayoutType } from '@abp/ng.core';

export const STATE_BASE_ROUTES: ABP.Route[] = [
  {
    name: '::Menu:Configurations',
    iconClass: 'fas fa-sliders-h',
    layout: eLayoutType.application,
    order: 4,
    //group: 'ModuleName::GroupName', // optional, for top-level grouping
  },
  {
    path: '/configurations/states',
    name: '::Menu:States',
    iconClass: 'fas fa-flag',
    parentName: '::Menu:Configurations',
    requiredPolicy: 'CaseEvaluation.States',
    order: 1,
  },
];
