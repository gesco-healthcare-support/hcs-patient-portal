FROM node:20-alpine AS build
WORKDIR /app
COPY angular/package.json angular/yarn.lock ./
RUN yarn install --frozen-lockfile
COPY angular/ .
RUN yarn build:prod

FROM nginx:alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/CaseEvaluation/browser /usr/share/nginx/html
EXPOSE 80
