FROM node:22-slim AS build
WORKDIR /app
COPY live-gateway/package.json live-gateway/package-lock.json ./
RUN npm ci
COPY shared ./shared
COPY functions/src ./functions/src
COPY live-gateway/src ./live-gateway/src
COPY live-gateway/tsconfig.json ./live-gateway/tsconfig.json
RUN ./node_modules/.bin/tsc -p live-gateway/tsconfig.json

FROM node:22-slim AS runtime
ENV NODE_ENV=production
WORKDIR /app
COPY live-gateway/package.json live-gateway/package-lock.json ./
RUN npm ci --omit=dev && npm cache clean --force
COPY --from=build /app/live-gateway/lib ./live-gateway/lib
USER node
CMD ["node", "live-gateway/lib/live-gateway/src/index.js"]
