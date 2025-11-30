# syntax=docker/dockerfile:1
FROM node:20-alpine AS build
WORKDIR /app
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
COPY package.json package-lock.json* ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build; npm run build:server

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY package.json package-lock.json* ./
RUN npm ci --omit=dev --no-audit --no-fund
COPY --from=build /app/dist /app/dist
COPY --from=build /app/build/server.js /app/server.js
EXPOSE 3000
ENV API_BASE_URL=${VITE_API_BASE_URL}
CMD ["node", "server.js"]