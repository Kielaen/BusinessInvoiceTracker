# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS dotnet-build
WORKDIR /source
COPY . .
RUN dotnet restore BusinessInvoiceTracker.slnx --locked-mode
RUN dotnet publish src/Invoice.Api/Invoice.Api.csproj --no-restore -c Release -o /out/api /p:UseAppHost=false
RUN dotnet publish src/Invoice.Automation/Invoice.Automation.csproj --no-restore -c Release -o /out/automation /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS api
WORKDIR /app
COPY --from=dotnet-build /out/api .
RUN mkdir -p /keys && chown "$APP_UID:$APP_UID" /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Invoice.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS automation
WORKDIR /app
COPY --from=dotnet-build /out/automation .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Invoice.Automation.dll"]

FROM node:24.14.1-alpine3.22@sha256:4f33c7804fa7775b87875f512e25e515692210bfdeafd715eb8bc441c364e2f6 AS web-build
WORKDIR /web
COPY web/invoice-web/package.json web/invoice-web/package-lock.json ./
RUN npm ci
COPY web/invoice-web/ .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.31.3-alpine3.24@sha256:a6c3ec0c0d249d68b0682df854d4a9e222b90fb607dc3fcf2f1d2fcbc85d347e AS web
COPY deploy/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=web-build /web/dist /usr/share/nginx/html
EXPOSE 8080
