# FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105-bionic AS build
FROM harbor.prlb.io/prlb-platform-dev/dotnet-sdk:bionic-20190327.3 AS build

COPY . /app
WORKDIR /app
RUN dotnet restore && dotnet publish --output /out

# FROM mcr.microsoft.com/dotnet/core/aspnet:2.2.3-bionic
FROM harbor.prlb.io/prlb-platform-dev/dotnet:2.2.3-aspnetcore-runtime-bionic-20190329.9
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT [ "dotnet", "thycotic-sdk-issue.dll" ]