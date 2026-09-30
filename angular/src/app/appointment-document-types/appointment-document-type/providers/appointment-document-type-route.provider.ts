import { inject, provideAppInitializer } from '@angular/core';
import { ABP, RoutesService } from '@abp/ng.core';
import { APPOINTMENT_DOCUMENT_TYPE_BASE_ROUTES } from './appointment-document-type-base.routes';

export const APPOINTMENT_DOCUMENT_TYPES_APPOINTMENT_DOCUMENT_TYPE_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routesService = inject(RoutesService);
  const routes: ABP.Route[] = [...APPOINTMENT_DOCUMENT_TYPE_BASE_ROUTES];
  routesService.add(routes);
}
