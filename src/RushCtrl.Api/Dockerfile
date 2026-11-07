FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/RushCtrl.Api/ src/RushCtrl.Api/
RUN dotnet publish src/RushCtrl.Api -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24 AS runtime
RUN apk add --no-cache tzdata
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RushCtrl.Api.dll"]
