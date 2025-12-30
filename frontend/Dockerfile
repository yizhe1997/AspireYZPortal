# Build static Vite React app and serve with nginx
FROM node:20.19-alpine AS build
WORKDIR /app

# install deps
COPY frontend/package*.json ./
COPY frontend/package-lock.json ./
RUN npm ci --silent

# copy source and build
COPY frontend/ ./
RUN npm run build

# Serve with nginx
FROM nginx:stable-alpine AS release
COPY --from=build /app/dist /usr/share/nginx/html
# Remove default nginx.conf and use a simple one to serve SPA (fallback to index.html)
RUN rm /etc/nginx/conf.d/default.conf
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY frontend/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
