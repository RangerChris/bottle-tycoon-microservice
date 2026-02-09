# syntax=docker/dockerfile:1
FROM dhi.io/node:25-debian13-dev AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci --omit=dev --no-audit --no-fund
COPY dist ./dist
COPY build/server.js ./server.js
EXPOSE 3000
ENV NODE_ENV=production
CMD ["node", "server.js"]