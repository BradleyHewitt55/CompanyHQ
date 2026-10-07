FROM node:22-alpine AS build
WORKDIR /app/Apps/Web
COPY Apps/Web/package*.json ./
RUN npm ci
COPY Assets /app/Assets
COPY Apps/Web ./
RUN npm run build

FROM node:22-alpine
WORKDIR /app/Apps/Web
ENV NODE_ENV=production
COPY --from=build /app/Apps/Web ./
COPY --from=build /app/Assets /app/Assets
EXPOSE 3104
CMD ["node", "node_modules/next/dist/bin/next", "start", "--hostname", "0.0.0.0", "--port", "3104"]
