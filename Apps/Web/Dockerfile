FROM node:22-alpine AS dependencies

# Keep the repository layout because the app reads shared assets from ../../Assets at build and
# runtime, just like the Marketing image.
WORKDIR /app/Apps/Web

COPY Apps/Web/package.json Apps/Web/package-lock.json ./
RUN npm ci

FROM node:22-alpine AS builder

WORKDIR /app/Apps/Web

COPY --from=dependencies /app/Apps/Web/node_modules ./node_modules
COPY Assets/Logo/ /app/Assets/Logo/
COPY Assets/Icons/ /app/Assets/Icons/
COPY Assets/Styles/ /app/Assets/Styles/
COPY Apps/Web/ ./

ENV NODE_ENV=production
RUN npm run build

FROM node:22-alpine AS runner

WORKDIR /app/Apps/Web

ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0
ENV PORT=3004

COPY --from=builder /app/Apps/Web/.next/standalone ./
COPY --from=builder /app/Apps/Web/.next/static ./.next/static
COPY --from=builder /app/Assets/Logo /app/Assets/Logo
COPY --from=builder /app/Assets/Icons /app/Assets/Icons
COPY --from=builder /app/Assets/Styles /app/Assets/Styles

EXPOSE 3004

CMD ["node", "server.js"]
