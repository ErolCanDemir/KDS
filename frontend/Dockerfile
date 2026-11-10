# LST-KDS önyüz — Vite SPA build → nginx statik sunum. Build bağlamı: frontend/
FROM node:22-alpine AS build
WORKDIR /uygulama
COPY package*.json ./
RUN npm ci
COPY . .
# Vite env BUILD-zamanıdır (tarayıcının göreceği adresler). Compose build args'tan gelir;
# URL değişirse imaj yeniden derlenir (tek-VM için kabul; runtime enjeksiyon gelecekte).
ARG VITE_API_ADRES
ARG VITE_KIMLIK_YETKILI
ARG VITE_KIMLIK_ISTEMCI
ENV VITE_API_ADRES=$VITE_API_ADRES \
    VITE_KIMLIK_YETKILI=$VITE_KIMLIK_YETKILI \
    VITE_KIMLIK_ISTEMCI=$VITE_KIMLIK_ISTEMCI
RUN npm run build

FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /uygulama/dist /usr/share/nginx/html
EXPOSE 80
