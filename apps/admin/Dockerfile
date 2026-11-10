FROM node:22-alpine AS build

WORKDIR /app
RUN corepack enable

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .

ARG VITE_API_BASE_URL=/api/v1
ARG VITE_IDP_URL=https://nerves.example.com
ARG VITE_OIDC_CLIENT_ID=nerves-admin
ARG VITE_USE_MOCK=false
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_IDP_URL=$VITE_IDP_URL \
    VITE_OIDC_CLIENT_ID=$VITE_OIDC_CLIENT_ID \
    VITE_USE_MOCK=$VITE_USE_MOCK
RUN pnpm build

FROM nginx:1.27-alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
# Build output can carry a restrictive umask; nginx workers must read it.
RUN chmod -R a+rX /usr/share/nginx/html

EXPOSE 8080
