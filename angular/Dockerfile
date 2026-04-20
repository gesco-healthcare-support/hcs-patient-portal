FROM node:20-alpine AS build

WORKDIR /app
COPY package.json yarn.lock /app/

RUN yarn install --frozen-lockfile
COPY . /app

ARG NG_CONFIG=docker
RUN npx ng build --configuration "${NG_CONFIG}"

FROM nginx:alpine
COPY dynamic-env.json /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/CaseEvaluation/browser /usr/share/nginx/html
