# Korkut SPA container — builds the Vite bundle, then serves it from nginx as its own tier.
# nginx also reverse-proxies /api to the backend service (see nginx.conf), so the browser talks
# to a single origin and same-origin session cookies + the Google OAuth callback just work.
# Build context is this repo root (see ../docker-compose.yml: ui.build.context = ./korkut-ui).

FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
# The backend is the .NET stack, so build the SPA in "dotnet" mode: this surfaces the
# local e-mail/parola sign-in (LocalAuth) and picks the .NET auth endpoint paths.
ARG VITE_API_MODE=dotnet
ENV VITE_API_MODE=$VITE_API_MODE
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
