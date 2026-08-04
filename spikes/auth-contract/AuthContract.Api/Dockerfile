FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS build
WORKDIR /src
COPY AuthContract.Api.csproj packages.lock.json ./
RUN dotnet restore --locked-mode
COPY . .
RUN dotnet publish --no-restore -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble@sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7 AS final
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /keys && chown "$APP_UID:$APP_UID" /keys
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AuthContract.Api.dll"]
