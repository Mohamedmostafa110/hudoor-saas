# ==============================================================================
# Multi-stage Dockerfile for hudoor-web (React 19 + TypeScript + Vite)
# ==============================================================================
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files for dependency caching
COPY package.json package-lock.json ./
RUN npm ci

# Copy source code and build for production
COPY . .
RUN npm run build

# ==============================================================================
# Nginx Runtime Stage
# ==============================================================================
FROM nginx:alpine AS runtime

# Copy custom Nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy compiled static assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
