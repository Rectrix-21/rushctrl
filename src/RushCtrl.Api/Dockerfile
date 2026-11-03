FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/RushCtrl.Api/ src/RushCtrl.Api/
RUN dotnet publish src/RushCtrl.Api -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RushCtrl.Api.dll"]
