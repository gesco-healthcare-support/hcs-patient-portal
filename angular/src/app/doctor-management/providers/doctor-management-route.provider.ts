import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { DOCTOR_MANAGEMENT_BASE_ROUTES } from './doctor-management-base.routes';

export const DOCTOR_MANAGEMENT_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...DOCTOR_MANAGEMENT_BASE_ROUTES];
  routesService.add(routes);
}
