FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["global.json", "global.json"]
COPY ["NuGet.config", "NuGet.config"]
COPY ["BookSomeSpace.sln", "BookSomeSpace.sln"]
COPY ["BookSomeSpace/BookSomeSpace.csproj", "BookSomeSpace/"]
RUN dotnet restore "BookSomeSpace.sln"
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -r linux-x64 --self-contained true -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BookSomeSpace.dll"]