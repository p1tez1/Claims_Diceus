# syntax=docker/dockerfile:1
FROM node:20-alpine AS build
WORKDIR /app
COPY frontend/claims-ui/package.json frontend/claims-ui/package-lock.json ./
RUN npm ci
COPY frontend/claims-ui/ ./
ARG API_BASE_URL=http://localhost:8080
ENV API_BASE_URL=$API_BASE_URL
RUN node tools/configure-production-environment.mjs && npm run build -- --configuration production

FROM nginx:1.27-alpine AS runtime
COPY frontend/claims-ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/claims-ui/browser /usr/share/nginx/html
EXPOSE 80

