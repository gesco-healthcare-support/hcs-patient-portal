import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { APPOINTMENT_LANGUAGE_BASE_ROUTES } from './appointment-language-base.routes';

export const APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...APPOINTMENT_LANGUAGE_BASE_ROUTES];
  routesService.add(routes);
}
