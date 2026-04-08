import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { DOCTOR_BASE_ROUTES } from './doctor-base.routes';

export const DOCTORS_DOCTOR_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...DOCTOR_BASE_ROUTES];
  routesService.add(routes);
}
