FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/business-workflow-engine-web/browser /usr/share/nginx/html
HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 CMD wget -q -O /dev/null http://127.0.0.1/healthz || exit 1
EXPOSE 80
