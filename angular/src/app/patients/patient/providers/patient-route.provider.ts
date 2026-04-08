import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { PATIENT_BASE_ROUTES } from './patient-base.routes';

export const PATIENTS_PATIENT_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...PATIENT_BASE_ROUTES];
  routesService.add(routes);
}
