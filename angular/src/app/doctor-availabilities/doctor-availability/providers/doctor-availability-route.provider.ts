import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { DOCTOR_AVAILABILITY_BASE_ROUTES } from './doctor-availability-base.routes';

export const DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...DOCTOR_AVAILABILITY_BASE_ROUTES];
  routesService.add(routes);
}
