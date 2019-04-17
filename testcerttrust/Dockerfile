FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105-bionic AS build
COPY testcerttrust /app
WORKDIR /app
RUN dotnet restore && dotnet publish --output /out

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2.3-bionic
COPY certs/root-ca.crt /usr/local/share/ca-certificates/root-ca.crt
COPY certs/signing-ca.crt /usr/local/share/ca-certificates/signing-ca.crt
RUN update-ca-certificates
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT [ "dotnet", "testcerttrust.dll" ]