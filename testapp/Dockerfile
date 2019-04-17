FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105-bionic AS build
COPY certs/app.pfx /certs/app.pfx
COPY testapp /app
WORKDIR /app
RUN dotnet restore && dotnet publish --output /out

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2.3-bionic
WORKDIR /app
EXPOSE 443
COPY --from=build /out .
ENTRYPOINT [ "dotnet", "testapp.dll" ]